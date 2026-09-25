using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Text;

/// <summary>
/// Parser for plain text (.txt) files.
/// Produces a single section containing the entire file content.
/// </summary>
public class TextParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".txt"];

    public bool CanParse(string extension)
        => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');

        return new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = Path.GetFullPath(filePath),
            Extension = ".txt",
            DocumentType = "document",
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
