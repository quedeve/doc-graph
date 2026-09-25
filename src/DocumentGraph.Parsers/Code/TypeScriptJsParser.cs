using System.Text.RegularExpressions;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Code;

/// <summary>
/// Parses TypeScript (.ts, .tsx) and JavaScript (.js, .jsx) files extracting:
/// classes, interfaces, types, functions, methods, and exported variables/constants.
/// </summary>
public class TypeScriptJsParser : IDocumentParser
{
    private static readonly string[] Supported = [".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    // Regex patterns for symbols
    private static readonly Regex ClassRegex = new(
        @"^\s*(?:export\s+)?(?:abstract\s+)?class\s+([A-Za-z0-9_$]+)(?:<[^>]+>)?(?:\s+extends\s+[^{]+)?(?:\s+implements\s+[^{]+)?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex InterfaceRegex = new(
        @"^\s*(?:export\s+)?interface\s+([A-Za-z0-9_$]+)(?:<[^>]+>)?(?:\s+extends\s+[^{]+)?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex TypeRegex = new(
        @"^\s*(?:export\s+)?type\s+([A-Za-z0-9_$]+)(?:<[^>]+>)?\s*=",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex FunctionRegex = new(
        @"^\s*(?:export\s+)?(?:async\s+)?function(?:\s*\*|\s+)([A-Za-z0-9_$]+)\s*(?:<[^>]+>)?\s*\(([^)]*)\)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ArrowFunctionRegex = new(
        @"^\s*(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_$]+)\s*=\s*(?:async\s*)?\(([^)]*)\)\s*(?::\s*[^=]+)?\s*=>",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var isTs = ext is ".ts" or ".tsx";

        var doc = new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = filePath,
            Extension = ext,
            DocumentType = "code",
            Language = isTs ? "typescript" : "javascript"
        };

        var lines = code.Split('\n');

        // Extract symbols
        ExtractRegexSymbols(lines, ClassRegex, SymbolKind.Class, doc.Symbols);
        ExtractRegexSymbols(lines, InterfaceRegex, SymbolKind.Interface, doc.Symbols);
        ExtractRegexSymbols(lines, TypeRegex, SymbolKind.Type, doc.Symbols);
        ExtractRegexSymbols(lines, FunctionRegex, SymbolKind.Function, doc.Symbols);
        ExtractRegexSymbols(lines, ArrowFunctionRegex, SymbolKind.Function, doc.Symbols);

        // Sort symbols by line start
        doc.Symbols.Sort((a, b) => a.LineStart.CompareTo(b.LineStart));

        // Create sections from symbols
        if (doc.Symbols.Count > 0)
        {
            foreach (var sym in doc.Symbols)
            {
                doc.Sections.Add(new DocumentSection
                {
                    Title = $"{sym.Kind}: {sym.Name}",
                    Content = sym.Content,
                    LineStart = sym.LineStart,
                    LineEnd = sym.LineEnd,
                    Depth = 1,
                    Metadata =
                    {
                        ["symbolKind"] = sym.Kind.ToString(),
                        ["symbolName"] = sym.Name
                    }
                });
            }
        }
        else
        {
            doc.Sections.Add(new DocumentSection
            {
                Title = Path.GetFileName(filePath),
                Content = code,
                LineStart = 1,
                LineEnd = lines.Length,
                Depth = 1
            });
        }

        return doc;
    }

    private static void ExtractRegexSymbols(
        string[] lines,
        Regex regex,
        SymbolKind kind,
        List<CodeSymbol> symbols)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var match = regex.Match(lines[i]);
            if (!match.Success)
                continue;

            var name = match.Groups[1].Value;
            int startLine = i + 1;
            int endLine = FindScopeEnd(lines, i);

            var contentLines = lines[i..Math.Min(endLine, lines.Length)];
            var content = string.Join("\n", contentLines);

            symbols.Add(new CodeSymbol
            {
                Name = name,
                Kind = kind,
                Signature = lines[i].Trim(),
                LineStart = startLine,
                LineEnd = endLine,
                Content = content
            });
        }
    }

    private static int FindScopeEnd(string[] lines, int startIndex)
    {
        int braceDepth = 0;
        bool foundOpenBrace = false;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (char c in line)
            {
                if (c == '{')
                {
                    braceDepth++;
                    foundOpenBrace = true;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (foundOpenBrace && braceDepth <= 0)
                        return i + 1;
                }
            }

            if (!foundOpenBrace && line.TrimEnd().EndsWith(';'))
            {
                return i + 1;
            }
        }

        return Math.Min(startIndex + 20, lines.Length);
    }
}
