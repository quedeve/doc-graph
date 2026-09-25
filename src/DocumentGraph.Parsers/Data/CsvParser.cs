using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Data;

/// <summary>
/// Parser for CSV (.csv) files.
/// Produces a single section containing a structured representation of the data,
/// with header row and row groups.
/// </summary>
public class CsvParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".csv"];

    public bool CanParse(string extension)
        => extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        var document = new ParsedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            SourcePath = Path.GetFullPath(filePath),
            Extension = ".csv",
            DocumentType = "data"
        };

        if (lines.Length == 0)
            return document;

        // Extract header
        var header = lines[0];
        var columnCount = header.Split(',').Length;

        document.Metadata["column_count"] = columnCount;
        document.Metadata["row_count"] = lines.Length - 1;
        document.Metadata["headers"] = header;

        // Create sections from row groups
        // Group rows into chunks of ~50 for manageable section sizes
        const int rowsPerGroup = 50;
        var sectionIndex = 0;

        // Header section
        document.Sections.Add(new DocumentSection
        {
            Title = "Headers",
            Content = header,
            Depth = 0,
            SectionIndex = sectionIndex++,
            LineStart = 1,
            LineEnd = 1
        });

        // Data sections in groups
        for (int i = 1; i < lines.Length; i += rowsPerGroup)
        {
            var endRow = Math.Min(i + rowsPerGroup, lines.Length);
            var groupLines = lines[i..endRow];
            var groupContent = $"{header}\n{string.Join('\n', groupLines)}";

            document.Sections.Add(new DocumentSection
            {
                Title = $"Rows {i}-{endRow - 1}",
                Content = groupContent,
                Depth = 1,
                SectionIndex = sectionIndex++,
                LineStart = i + 1,
                LineEnd = endRow
            });
        }

        return document;
    }
}
