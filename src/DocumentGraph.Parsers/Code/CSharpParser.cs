using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using SymbolKind = DocumentGraph.Core.Models.SymbolKind;

namespace DocumentGraph.Parsers.Code;

/// <summary>
/// Parses C# source files using Roslyn to extract full AST symbol hierarchy:
/// Namespaces, Classes, Structs, Interfaces, Records, Enums, Methods, Constructors, and Properties.
/// </summary>
public class CSharpParser : IDocumentParser
{
    private static readonly string[] Supported = [".cs"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        var doc = new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = filePath,
            Extension = ".cs",
            DocumentType = "code",
            Language = "csharp"
        };

        var symbols = new List<CodeSymbol>();
        var visitor = new CSharpSymbolVisitor(code);
        visitor.Visit(root);

        doc.Symbols = visitor.TopLevelSymbols;

        // Create logical sections from top-level types (classes, interfaces, etc.)
        if (doc.Symbols.Count > 0)
        {
            foreach (var sym in doc.Symbols)
            {
                AddSectionsFromSymbol(sym, doc.Sections);
            }
        }
        else
        {
            // Fallback: whole file as section
            doc.Sections.Add(new DocumentSection
            {
                Title = Path.GetFileName(filePath),
                Content = code,
                LineStart = 1,
                LineEnd = code.Split('\n').Length,
                Depth = 1
            });
        }

        return doc;
    }

    private static void AddSectionsFromSymbol(CodeSymbol symbol, List<DocumentSection> sections)
    {
        sections.Add(new DocumentSection
        {
            Title = $"{symbol.Kind}: {symbol.Name}",
            Content = symbol.Content,
            LineStart = symbol.LineStart,
            LineEnd = symbol.LineEnd,
            Depth = 1,
            Metadata =
            {
                ["symbolKind"] = symbol.Kind.ToString(),
                ["symbolName"] = symbol.Name,
                ["namespace"] = symbol.Namespace ?? string.Empty
            }
        });

        foreach (var child in symbol.Children)
        {
            if (child.Kind is SymbolKind.Method or SymbolKind.Constructor or SymbolKind.Property)
            {
                sections.Add(new DocumentSection
                {
                    Title = $"{symbol.Name}.{child.Name}",
                    Content = child.Content,
                    LineStart = child.LineStart,
                    LineEnd = child.LineEnd,
                    Depth = 2,
                    Metadata =
                    {
                        ["symbolKind"] = child.Kind.ToString(),
                        ["symbolName"] = child.Name,
                        ["parentSymbol"] = symbol.Name
                    }
                });
            }
        }
    }
}

internal class CSharpSymbolVisitor : CSharpSyntaxWalker
{
    private readonly string _sourceCode;
    public List<CodeSymbol> TopLevelSymbols { get; } = [];
    private readonly Stack<CodeSymbol> _scopeStack = new();
    private string _currentNamespace = string.Empty;

    public CSharpSymbolVisitor(string sourceCode)
    {
        _sourceCode = sourceCode;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var prevNs = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = prevNs;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        HandleTypeDeclaration(node, node.Identifier.Text, SymbolKind.Class, () => base.VisitClassDeclaration(node));
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        HandleTypeDeclaration(node, node.Identifier.Text, SymbolKind.Interface, () => base.VisitInterfaceDeclaration(node));
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        HandleTypeDeclaration(node, node.Identifier.Text, SymbolKind.Struct, () => base.VisitStructDeclaration(node));
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        HandleTypeDeclaration(node, node.Identifier.Text, SymbolKind.Class, () => base.VisitRecordDeclaration(node));
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        HandleTypeDeclaration(node, node.Identifier.Text, SymbolKind.Enum, () => base.VisitEnumDeclaration(node));
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var symbol = new CodeSymbol
        {
            Name = node.Identifier.Text,
            Kind = SymbolKind.Method,
            Namespace = _currentNamespace,
            Signature = $"{node.ReturnType} {node.Identifier}{node.ParameterList}",
            LineStart = lineSpan.StartLinePosition.Line + 1,
            LineEnd = lineSpan.EndLinePosition.Line + 1,
            Content = node.ToString()
        };

        AddSymbol(symbol);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var symbol = new CodeSymbol
        {
            Name = node.Identifier.Text,
            Kind = SymbolKind.Constructor,
            Namespace = _currentNamespace,
            Signature = $"{node.Identifier}{node.ParameterList}",
            LineStart = lineSpan.StartLinePosition.Line + 1,
            LineEnd = lineSpan.EndLinePosition.Line + 1,
            Content = node.ToString()
        };

        AddSymbol(symbol);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var symbol = new CodeSymbol
        {
            Name = node.Identifier.Text,
            Kind = SymbolKind.Property,
            Namespace = _currentNamespace,
            Signature = $"{node.Type} {node.Identifier}",
            LineStart = lineSpan.StartLinePosition.Line + 1,
            LineEnd = lineSpan.EndLinePosition.Line + 1,
            Content = node.ToString()
        };

        AddSymbol(symbol);
        base.VisitPropertyDeclaration(node);
    }

    private void HandleTypeDeclaration(SyntaxNode node, string name, SymbolKind kind, Action visitChildren)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var symbol = new CodeSymbol
        {
            Name = name,
            Kind = kind,
            Namespace = _currentNamespace,
            LineStart = lineSpan.StartLinePosition.Line + 1,
            LineEnd = lineSpan.EndLinePosition.Line + 1,
            Content = node.ToString()
        };

        AddSymbol(symbol);
        _scopeStack.Push(symbol);
        visitChildren();
        _scopeStack.Pop();
    }

    private void AddSymbol(CodeSymbol symbol)
    {
        if (_scopeStack.Count > 0)
        {
            _scopeStack.Peek().Children.Add(symbol);
        }
        else
        {
            TopLevelSymbols.Add(symbol);
        }
    }
}
