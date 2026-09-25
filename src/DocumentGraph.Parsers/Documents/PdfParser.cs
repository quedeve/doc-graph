using UglyToad.PdfPig;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Documents;

/// <summary>
/// Parses PDF (.pdf) documents extracting text, page structure, and metadata.
/// </summary>
public class PdfParser : IDocumentParser
{
    private static readonly string[] Supported = [".pdf"];

    public IReadOnlyList<string> SupportedExtensions => Supported;

    public bool CanParse(string extension) => Supported.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var doc = new ParsedDocument
        {
            SourcePath = filePath,
            Extension = Path.GetExtension(filePath).ToLowerInvariant(),
            DocumentType = "pdf"
        };

        using var pdf = PdfDocument.Open(filePath);

        // Document level metadata
        if (pdf.Information != null)
        {
            if (!string.IsNullOrWhiteSpace(pdf.Information.Title))
                doc.Title = pdf.Information.Title;
            if (!string.IsNullOrWhiteSpace(pdf.Information.Author))
                doc.Metadata["author"] = pdf.Information.Author;
            if (!string.IsNullOrWhiteSpace(pdf.Information.Subject))
                doc.Metadata["subject"] = pdf.Information.Subject;
            if (!string.IsNullOrWhiteSpace(pdf.Information.Keywords))
                doc.Metadata["keywords"] = pdf.Information.Keywords;
            if (!string.IsNullOrWhiteSpace(pdf.Information.Creator))
                doc.Metadata["creator"] = pdf.Information.Creator;
            if (!string.IsNullOrWhiteSpace(pdf.Information.Producer))
                doc.Metadata["producer"] = pdf.Information.Producer;
        }

        if (string.IsNullOrWhiteSpace(doc.Title))
        {
            doc.Title = Path.GetFileNameWithoutExtension(filePath);
        }

        doc.Metadata["pageCount"] = pdf.NumberOfPages.ToString();

        // Page-by-page section extraction
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            doc.Sections.Add(new DocumentSection
            {
                Title = $"Page {page.Number}",
                PageNumber = page.Number,
                Content = text.Trim(),
                Depth = 1,
                Metadata =
                {
                    ["pageWidth"] = page.Width.ToString("F0"),
                    ["pageHeight"] = page.Height.ToString("F0")
                }
            });
        }

        return Task.FromResult(doc);
    }
}
