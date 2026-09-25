using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Data;

/// <summary>
/// Parser for JSON (.json) files.
/// Treats the file as a document with a single section.
/// </summary>
public class JsonParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".json"];

    public bool CanParse(string extension)
        => extension.Equals(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');

        return new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = Path.GetFullPath(filePath),
            Extension = ".json",
            DocumentType = "data",
            Sections =
            [
                new DocumentSection
                {
                    Title = Path.GetFileName(filePath),
                    Content = content,
                    Depth = 0,
                    SectionIndex = 0,
                    LineStart = 1,
                    LineEnd = lines.Length
                }
            ]
        };
    }
}

/// <summary>
/// Parser for XML (.xml) files.
/// Treats the file as a document with a single section.
/// </summary>
public class XmlParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".xml"];

    public bool CanParse(string extension)
        => extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');

        return new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = Path.GetFullPath(filePath),
            Extension = ".xml",
            DocumentType = "data",
            Sections =
            [
                new DocumentSection
                {
                    Title = Path.GetFileName(filePath),
                    Content = content,
                    Depth = 0,
                    SectionIndex = 0,
                    LineStart = 1,
                    LineEnd = lines.Length
                }
            ]
        };
    }
}
