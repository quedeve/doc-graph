using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocumentGraph.Parsers.Documents;

/// <summary>
/// Parses PowerPoint (.pptx, .ppt) presentations extracting slide titles, body text, notes, and tables.
/// </summary>
public class PptxParser : IDocumentParser
{
    private static readonly string[] Supported = [".pptx", ".ppt"];

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
            using var presentationDoc = PresentationDocument.Open(filePath, false);

            var props = presentationDoc.PackageProperties;
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

            var presentationPart = presentationDoc.PresentationPart;
            if (presentationPart?.Presentation?.SlideIdList == null)
                return Task.FromResult(doc);

            int slideNumber = 1;
            var slideIds = presentationPart.Presentation.SlideIdList.Elements<SlideId>();

            foreach (var slideId in slideIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relId = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relId))
                {
                    slideNumber++;
                    continue;
                }

                if (presentationPart.GetPartById(relId) is not SlidePart slidePart)
                {
                    slideNumber++;
                    continue;
                }

                var slideText = new StringBuilder();
                string? slideTitle = null;

                // Extract text from slide shapes
                if (slidePart.Slide?.CommonSlideData?.ShapeTree != null)
                {
                    foreach (var shape in slidePart.Slide.CommonSlideData.ShapeTree.Elements<Shape>())
                    {
                        var textBody = shape.TextBody;
                        if (textBody == null) continue;

                        var isTitle = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?
                            .PlaceholderShape?.Type?.Value == PlaceholderValues.Title ||
                            shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?
                            .PlaceholderShape?.Type?.Value == PlaceholderValues.CenteredTitle;

                        var paragraphs = textBody.Elements<A.Paragraph>()
                            .Select(p => p.InnerText.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();

                        if (paragraphs.Count == 0) continue;

                        if (isTitle && slideTitle == null)
                        {
                            slideTitle = string.Join(" ", paragraphs);
                        }
                        else
                        {
                            foreach (var p in paragraphs)
                            {
                                slideText.AppendLine(p);
                            }
                        }
                    }
                }

                // Extract tables if present
                if (slidePart.Slide?.CommonSlideData?.ShapeTree != null)
                {
                    foreach (var graphicFrame in slidePart.Slide.CommonSlideData.ShapeTree.Elements<GraphicFrame>())
                    {
                        var table = graphicFrame.Graphic?.GraphicData?.GetFirstChild<A.Table>();
                        if (table == null) continue;

                        foreach (var row in table.Elements<A.TableRow>())
                        {
                            var cells = row.Elements<A.TableCell>().Select(c => c.TextBody?.InnerText.Trim() ?? string.Empty);
                            slideText.AppendLine(string.Join(" | ", cells));
                        }
                    }
                }

                // Extract notes slide if present
                if (slidePart.NotesSlidePart?.NotesSlide != null)
                {
                    var noteText = slidePart.NotesSlidePart.NotesSlide.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(noteText))
                    {
                        slideText.AppendLine();
                        slideText.AppendLine($"[Notes: {noteText}]");
                    }
                }

                var title = slideTitle ?? $"Slide {slideNumber}";
                var content = slideText.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(content) || !string.IsNullOrWhiteSpace(slideTitle))
                {
                    doc.Sections.Add(new DocumentSection
                    {
                        Title = title,
                        SlideNumber = slideNumber,
                        Content = string.IsNullOrWhiteSpace(content) ? title : $"{title}\n{content}",
                        Depth = 1
                    });
                }

                slideNumber++;
            }

            doc.Metadata["slideCount"] = (slideNumber - 1).ToString();
            return Task.FromResult(doc);
        }
        catch (Exception)
        {
            // Fallback for legacy binary .ppt format
            if (File.Exists(filePath))
            {
                var bytes = File.ReadAllBytes(filePath);
                var extracted = LegacyOfficeExtractor.ExtractTextStrings(bytes);
                if (extracted.Count > 0)
                {
                    doc.Title = Path.GetFileNameWithoutExtension(filePath);
                    doc.Metadata["legacy_format"] = "true";
                    int slideNum = 1;
                    foreach (var chunk in extracted.Chunk(3))
                    {
                        doc.Sections.Add(new DocumentSection
                        {
                            Title = $"Slide {slideNum}",
                            SlideNumber = slideNum,
                            Content = string.Join("\n", chunk),
                            Depth = 1
                        });
                        slideNum++;
                    }
                    doc.Metadata["slideCount"] = (slideNum - 1).ToString();
                }
            }

            return Task.FromResult(doc);
        }
    }
}
