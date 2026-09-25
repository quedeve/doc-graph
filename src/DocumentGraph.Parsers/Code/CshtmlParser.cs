using System.Text.RegularExpressions;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Code;

/// <summary>
/// Parses ASP.NET Razor (.cshtml, .razor) files extracting page directives, model definitions,
/// code blocks (@functions, @code), and markup sections.
/// </summary>
public class CshtmlParser : IDocumentParser
{
    private static readonly string[] Supported = [".cshtml", ".razor"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex ModelRegex = new(
        @"@model\s+([A-Za-z0-9_<>.,\s]+)",
        RegexOptions.Compiled);

    private static readonly Regex PageRegex = new(
        @"@page(?:\s+""([^""]+)"")?",
        RegexOptions.Compiled);

    private static readonly Regex CodeBlockRegex = new(
        @"@(?:code|functions)\s*\{",
        RegexOptions.Compiled);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var doc = new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = filePath,
            Extension = Path.GetExtension(filePath).ToLowerInvariant(),
            DocumentType = "code",
            Language = "cshtml"
        };

        var lines = content.Split('\n');

        // Check @page and @model
        var pageMatch = PageRegex.Match(content);
        if (pageMatch.Success)
        {
            doc.Metadata["route"] = pageMatch.Groups[1].Success ? pageMatch.Groups[1].Value : "/";
        }

        var modelMatch = ModelRegex.Match(content);
        if (modelMatch.Success)
        {
            var modelType = modelMatch.Groups[1].Value.Trim();
            doc.Metadata["model"] = modelType;
            doc.Symbols.Add(new CodeSymbol
            {
                Name = modelType,
                Kind = SymbolKind.Class,
                Signature = $"@model {modelType}",
                LineStart = 1,
                LineEnd = 1,
                Content = $"@model {modelType}"
            });
        }

        // Section for Razor directives / header
        doc.Sections.Add(new DocumentSection
        {
            Title = "Template View",
            Content = content,
            LineStart = 1,
            LineEnd = lines.Length,
            Depth = 1,
            Metadata =
            {
                ["hasCodeBlock"] = CodeBlockRegex.IsMatch(content).ToString()
            }
        });

        return doc;
    }
}
