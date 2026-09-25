using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Documents;

/// <summary>
/// Parses Word (.docx) documents preserving heading hierarchy, paragraphs, and tables.
/// </summary>
public class DocxParser : IDocumentParser
{
    private static readonly string[] Supported = [".docx", ".doc"];

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
            using var wordDoc = WordprocessingDocument.Open(filePath, false);

            // Core properties
            var props = wordDoc.PackageProperties;
            if (props != null)
            {
                if (!string.IsNullOrWhiteSpace(props.Title))
                    doc.Title = props.Title;
                if (!string.IsNullOrWhiteSpace(props.Creator))
                    doc.Metadata["author"] = props.Creator;
                if (!string.IsNullOrWhiteSpace(props.Subject))
                    doc.Metadata["subject"] = props.Subject;
                if (!string.IsNullOrWhiteSpace(props.Keywords))
                    doc.Metadata["keywords"] = props.Keywords;
                if (!string.IsNullOrWhiteSpace(props.Description))
                    doc.Metadata["description"] = props.Description;
            }

            if (string.IsNullOrWhiteSpace(doc.Title))
            {
                doc.Title = Path.GetFileNameWithoutExtension(filePath);
            }

            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return Task.FromResult(doc);

            var currentSection = new DocumentSection
            {
                Title = "Overview",
                Depth = 1
            };
            var currentSectionContent = new StringBuilder();

            foreach (var element in body.Elements())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (element is Paragraph p)
                {
                    var text = p.InnerText;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    // Check if paragraph is a heading
                    var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    int headingLevel = GetHeadingLevel(styleId);

                    if (headingLevel > 0)
                    {
                        // Flush accumulated section
                        if (currentSectionContent.Length > 0)
                        {
                            currentSection.Content = currentSectionContent.ToString().Trim();
                            doc.Sections.Add(currentSection);
                            currentSectionContent.Clear();
                        }

                        currentSection = new DocumentSection
                        {
                            Title = text.Trim(),
                            Depth = headingLevel
                        };
                    }
                    else
                    {
                        currentSectionContent.AppendLine(text);
                    }
                }
                else if (element is Table t)
                {
                    // Format table content as readable text / markdown rows
                    var tableText = ExtractTableText(t);
                    if (!string.IsNullOrWhiteSpace(tableText))
                    {
                        currentSectionContent.AppendLine(tableText);
                    }
                }
            }

            // Flush final section
            if (currentSectionContent.Length > 0)
            {
                currentSection.Content = currentSectionContent.ToString().Trim();
                doc.Sections.Add(currentSection);
            }

            return Task.FromResult(doc);
        }
        catch (Exception)
        {
            // Fallback for legacy binary .doc format
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
                        Depth = 1,
                        Content = string.Join("\n\n", extracted)
                    });
                }
            }

            return Task.FromResult(doc);
        }
    }

    private static int GetHeadingLevel(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return 0;

        // Common Word heading styles: "Heading1", "Heading2", "1", "2", "heading 1"
        var normalized = styleId.ToLowerInvariant().Replace(" ", "");
        if (normalized.StartsWith("heading") && normalized.Length > 7 && int.TryParse(normalized[7..], out int lvl))
        {
            return lvl;
        }

        return 0;
    }

    private static string ExtractTableText(Table table)
    {
        var sb = new StringBuilder();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().Select(c => c.InnerText.Trim());
            sb.AppendLine(string.Join(" | ", cells));
        }
        return sb.ToString().TrimEnd();
    }
}
