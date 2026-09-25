using System.Text.RegularExpressions;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Code;

/// <summary>
/// Parses SQL (.sql) files into statements, tables, procedures, views, and functions.
/// </summary>
public class SqlParser : IDocumentParser
{
    private static readonly string[] Supported = [".sql"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex TableRegex = new(
        @"(?i)CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
        RegexOptions.Compiled);

    private static readonly Regex ViewRegex = new(
        @"(?i)CREATE\s+(?:OR\s+REPLACE\s+)?VIEW\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
        RegexOptions.Compiled);

    private static readonly Regex ProcedureRegex = new(
        @"(?i)CREATE\s+(?:OR\s+REPLACE\s+)?(?:PROCEDURE|PROC)\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
        RegexOptions.Compiled);

    private static readonly Regex FunctionRegex = new(
        @"(?i)CREATE\s+(?:OR\s+REPLACE\s+)?FUNCTION\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
        RegexOptions.Compiled);

    private static readonly Regex IndexRegex = new(
        @"(?i)CREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?\[?(\w+)\]?\s+ON\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
        RegexOptions.Compiled);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var sql = await File.ReadAllTextAsync(filePath, cancellationToken);

        var doc = new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = filePath,
            Extension = ".sql",
            DocumentType = "code",
            Language = "sql"
        };

        var lines = sql.Split('\n');

        // Extract DDL symbols
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Tables
            var mTable = TableRegex.Match(line);
            if (mTable.Success)
            {
                var schema = mTable.Groups[1].Value;
                var tableName = string.IsNullOrEmpty(schema) ? mTable.Groups[2].Value : mTable.Groups[2].Value;
                var fullName = string.IsNullOrEmpty(schema) ? tableName : $"{schema}.{tableName}";
                int endLine = FindStatementEnd(lines, i);

                doc.Symbols.Add(new CodeSymbol
                {
                    Name = fullName,
                    Kind = SymbolKind.Class, // Best fit for Table in SymbolKind
                    Namespace = schema,
                    Signature = line.Trim(),
                    LineStart = i + 1,
                    LineEnd = endLine,
                    Content = string.Join("\n", lines[i..Math.Min(endLine, lines.Length)])
                });
                continue;
            }

            // Views
            var mView = ViewRegex.Match(line);
            if (mView.Success)
            {
                var schema = mView.Groups[1].Value;
                var viewName = mView.Groups[2].Value;
                var fullName = string.IsNullOrEmpty(schema) ? viewName : $"{schema}.{viewName}";
                int endLine = FindStatementEnd(lines, i);

                doc.Symbols.Add(new CodeSymbol
                {
                    Name = fullName,
                    Kind = SymbolKind.Type,
                    Namespace = schema,
                    Signature = line.Trim(),
                    LineStart = i + 1,
                    LineEnd = endLine,
                    Content = string.Join("\n", lines[i..Math.Min(endLine, lines.Length)])
                });
                continue;
            }

            // Procedures
            var mProc = ProcedureRegex.Match(line);
            if (mProc.Success)
            {
                var schema = mProc.Groups[1].Value;
                var procName = mProc.Groups[2].Value;
                var fullName = string.IsNullOrEmpty(schema) ? procName : $"{schema}.{procName}";
                int endLine = FindStatementEnd(lines, i);

                doc.Symbols.Add(new CodeSymbol
                {
                    Name = fullName,
                    Kind = SymbolKind.Method,
                    Namespace = schema,
                    Signature = line.Trim(),
                    LineStart = i + 1,
                    LineEnd = endLine,
                    Content = string.Join("\n", lines[i..Math.Min(endLine, lines.Length)])
                });
                continue;
            }

            // Functions
            var mFunc = FunctionRegex.Match(line);
            if (mFunc.Success)
            {
                var schema = mFunc.Groups[1].Value;
                var funcName = mFunc.Groups[2].Value;
                var fullName = string.IsNullOrEmpty(schema) ? funcName : $"{schema}.{funcName}";
                int endLine = FindStatementEnd(lines, i);

                doc.Symbols.Add(new CodeSymbol
                {
                    Name = fullName,
                    Kind = SymbolKind.Function,
                    Namespace = schema,
                    Signature = line.Trim(),
                    LineStart = i + 1,
                    LineEnd = endLine,
                    Content = string.Join("\n", lines[i..Math.Min(endLine, lines.Length)])
                });
                continue;
            }
        }

        // Create sections from symbols or whole file
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
                        ["symbolName"] = sym.Name,
                        ["symbolKind"] = sym.Kind.ToString()
                    }
                });
            }
        }
        else
        {
            doc.Sections.Add(new DocumentSection
            {
                Title = Path.GetFileName(filePath),
                Content = sql,
                LineStart = 1,
                LineEnd = lines.Length,
                Depth = 1
            });
        }

        return doc;
    }

    private static int FindStatementEnd(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimEnd();
            if (trimmed.EndsWith(';') || trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return Math.Min(startIndex + 50, lines.Length);
    }
}
