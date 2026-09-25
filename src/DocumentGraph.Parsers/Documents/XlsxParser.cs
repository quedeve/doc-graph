using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Documents;

/// <summary>
/// Parses Excel (.xlsx, .xls) workbooks into sections per sheet, formatted as readable tabular data.
/// </summary>
public class XlsxParser : IDocumentParser
{
    private static readonly string[] Supported = [".xlsx", ".xls"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var doc = new ParsedDocument
        {
            SourcePath = filePath,
            Extension = ext,
            DocumentType = ext.TrimStart('.')
        };

        try
        {
            using var spreadsheet = SpreadsheetDocument.Open(filePath, false);

            var props = spreadsheet.PackageProperties;
            if (props != null)
            {
                if (!string.IsNullOrWhiteSpace(props.Title))
                    doc.Title = props.Title;
                if (!string.IsNullOrWhiteSpace(props.Creator))
                    doc.Metadata["author"] = props.Creator;
                if (!string.IsNullOrWhiteSpace(props.Subject))
                    doc.Metadata["subject"] = props.Subject;
            }

            if (string.IsNullOrWhiteSpace(doc.Title))
            {
                doc.Title = Path.GetFileNameWithoutExtension(filePath);
            }

            var workbookPart = spreadsheet.WorkbookPart;
            if (workbookPart == null)
                return Task.FromResult(doc);

            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
            var sheets = workbookPart.Workbook?.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();

            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sheetName = sheet.Name?.Value ?? "Sheet";
                var relationshipId = sheet.Id?.Value;
                if (string.IsNullOrEmpty(relationshipId))
                    continue;

                if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                    continue;

                var sheetData = worksheetPart.Worksheet?.Elements<SheetData>().FirstOrDefault();
                if (sheetData == null)
                    continue;

                var sb = new StringBuilder();
                int rowCount = 0;

                foreach (var row in sheetData.Elements<Row>())
                {
                    var cellValues = new List<string>();
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var cellText = GetCellValue(cell, sharedStringTable);
                        cellValues.Add(cellText);
                    }

                    if (cellValues.Any(v => !string.IsNullOrEmpty(v)))
                    {
                        sb.AppendLine(string.Join(" | ", cellValues));
                        rowCount++;
                    }
                }

                var content = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                doc.Sections.Add(new DocumentSection
                {
                    Title = sheetName,
                    SheetName = sheetName,
                    Content = content,
                    Depth = 1,
                    Metadata =
                    {
                        ["rowCount"] = rowCount.ToString()
                    }
                });
            }

            return Task.FromResult(doc);
        }
        catch (Exception)
        {
            // Fallback for legacy binary .xls format
            if (File.Exists(filePath))
            {
                var bytes = File.ReadAllBytes(filePath);
                var extracted = LegacyOfficeExtractor.ExtractTextStrings(bytes);
                if (extracted.Count > 0)
                {
                    doc.Title = Path.GetFileNameWithoutExtension(filePath);
                    doc.Metadata["legacy_format"] = "true";
                    doc.Sections.Add(new DocumentSection
                    {
                        Title = doc.Title,
                        SheetName = "Sheet1",
                        Content = string.Join("\n", extracted),
                        Depth = 1
                    });
                }
            }

            return Task.FromResult(doc);
        }
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
    {
        var value = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            if (int.TryParse(value, out int id) && sharedStringTable != null)
            {
                var item = sharedStringTable.ElementAtOrDefault(id);
                return item?.InnerText?.Trim() ?? string.Empty;
            }
        }

        return value.Trim();
    }
}
