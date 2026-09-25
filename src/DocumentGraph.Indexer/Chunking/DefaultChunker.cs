using System.Text;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Indexer.Chunking;

/// <summary>
/// Text-based chunking with configurable token size, overlap, and logical section preservation.
/// </summary>
public class DefaultChunker : IChunker
{
    private static readonly HashSet<string> HandledTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "markdown", "csv", "json", "xml", "pdf", "docx", "xlsx", "pptx"
    };

    public bool CanChunk(string documentType) => HandledTypes.Contains(documentType);

    public List<DocumentChunk> Chunk(ParsedDocument document, ChunkingOptions? options = null)
    {
        options ??= new ChunkingOptions();
        var chunks = new List<DocumentChunk>();

        foreach (var section in document.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Content))
                continue;

            var sectionChunks = ChunkSection(section, options, chunks.Count);
            chunks.AddRange(sectionChunks);
        }

        return chunks;
    }

    private static List<DocumentChunk> ChunkSection(
        DocumentSection section,
        ChunkingOptions options,
        int startingChunkIndex)
    {
        var result = new List<DocumentChunk>();
        var content = section.Content.Trim();
        var estimatedTokens = EstimateTokens(content);

        // If the section fits in one chunk, keep it intact
        if (estimatedTokens <= options.MaxTokensPerChunk)
        {
            result.Add(new DocumentChunk
            {
                ChunkIndex = startingChunkIndex,
                Content = content,
                TokenCount = estimatedTokens,
                PageNumber = section.PageNumber,
                SlideNumber = section.SlideNumber,
                SheetName = section.SheetName,
                LineStart = section.LineStart,
                LineEnd = section.LineEnd,
                SectionTitle = section.Title
            });
            return result;
        }

        // Section exceeds max tokens: split by paragraphs/lines with overlap
        var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunkText = new StringBuilder();
        int currentChunkTokens = 0;

        foreach (var para in paragraphs)
        {
            var paraTokens = EstimateTokens(para);

            if (paraTokens > options.MaxTokensPerChunk)
            {
                var subParts = para.Split(new[] { "\r\n", "\n", ". " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in subParts)
                {
                    var partTokens = EstimateTokens(part);
                    if (currentChunkTokens + partTokens > options.MaxTokensPerChunk && currentChunkTokens > 0)
                    {
                        result.Add(new DocumentChunk
                        {
                            ChunkIndex = startingChunkIndex + result.Count,
                            Content = currentChunkText.ToString().Trim(),
                            TokenCount = currentChunkTokens,
                            PageNumber = section.PageNumber,
                            SlideNumber = section.SlideNumber,
                            SheetName = section.SheetName,
                            LineStart = section.LineStart,
                            LineEnd = section.LineEnd,
                            SectionTitle = section.Title
                        });
                        currentChunkText.Clear();
                        currentChunkTokens = 0;
                    }

                    if (currentChunkText.Length > 0)
                        currentChunkText.Append(". ");

                    currentChunkText.Append(part);
                    currentChunkTokens += partTokens;
                }
                continue;
            }

            if (currentChunkTokens + paraTokens > options.MaxTokensPerChunk && currentChunkTokens > 0)
            {
                // Emit current chunk
                result.Add(new DocumentChunk
                {
                    ChunkIndex = startingChunkIndex + result.Count,
                    Content = currentChunkText.ToString().Trim(),
                    TokenCount = currentChunkTokens,
                    PageNumber = section.PageNumber,
                    SlideNumber = section.SlideNumber,
                    SheetName = section.SheetName,
                    LineStart = section.LineStart,
                    LineEnd = section.LineEnd,
                    SectionTitle = section.Title
                });

                currentChunkText.Clear();
                currentChunkTokens = 0;
            }

            if (currentChunkText.Length > 0)
                currentChunkText.AppendLine().AppendLine();

            currentChunkText.Append(para);
            currentChunkTokens += paraTokens;
        }

        if (currentChunkText.Length > 0)
        {
            result.Add(new DocumentChunk
            {
                ChunkIndex = startingChunkIndex + result.Count,
                Content = currentChunkText.ToString().Trim(),
                TokenCount = currentChunkTokens,
                PageNumber = section.PageNumber,
                SlideNumber = section.SlideNumber,
                SheetName = section.SheetName,
                LineStart = section.LineStart,
                LineEnd = section.LineEnd,
                SectionTitle = section.Title
            });
        }

        return result;
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
