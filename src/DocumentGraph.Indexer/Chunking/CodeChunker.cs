using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Indexer.Chunking;

/// <summary>
/// Chunks code documents by logical code symbols (classes, methods, functions, interfaces),
/// preserving symbol hierarchy and line ranges.
/// </summary>
public class CodeChunker : IChunker
{
    private static readonly HashSet<string> HandledTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "code"
    };

    public bool CanChunk(string documentType) => HandledTypes.Contains(documentType);

    public List<DocumentChunk> Chunk(ParsedDocument document, ChunkingOptions? options = null)
    {
        options ??= new ChunkingOptions();
        var chunks = new List<DocumentChunk>();

        // If symbols exist, chunk by symbols
        if (document.Symbols.Count > 0)
        {
            foreach (var sym in document.Symbols)
            {
                ChunkSymbol(sym, document, options, chunks);
            }
        }
        else
        {
            // Fallback to sections if no symbols were identified
            foreach (var section in document.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Content))
                    continue;

                chunks.Add(new DocumentChunk
                {
                    ChunkIndex = chunks.Count,
                    Content = section.Content.Trim(),
                    TokenCount = EstimateTokens(section.Content),
                    LineStart = section.LineStart,
                    LineEnd = section.LineEnd,
                    SectionTitle = section.Title
                });
            }
        }

        return chunks;
    }

    private static void ChunkSymbol(
        CodeSymbol symbol,
        ParsedDocument document,
        ChunkingOptions options,
        List<DocumentChunk> chunks)
    {
        var tokenCount = EstimateTokens(symbol.Content);

        // If symbol fits nicely in chunk limit or has no children, create a chunk
        if (tokenCount <= options.MaxTokensPerChunk || symbol.Children.Count == 0)
        {
            chunks.Add(new DocumentChunk
            {
                ChunkIndex = chunks.Count,
                Content = symbol.Content.Trim(),
                TokenCount = tokenCount,
                LineStart = symbol.LineStart,
                LineEnd = symbol.LineEnd,
                SectionTitle = $"{symbol.Kind}: {symbol.Name}",
                Metadata =
                {
                    ["symbolName"] = symbol.Name,
                    ["symbolKind"] = symbol.Kind.ToString(),
                    ["namespace"] = symbol.Namespace ?? string.Empty
                }
            });
        }
        else
        {
            // Class/Struct/Interface is large and has child symbols (methods, properties):
            // Emit a declaration header chunk
            var header = $"{symbol.Signature ?? $"{symbol.Kind} {symbol.Name}"} (Line {symbol.LineStart}-{symbol.LineEnd})";
            chunks.Add(new DocumentChunk
            {
                ChunkIndex = chunks.Count,
                Content = header,
                TokenCount = EstimateTokens(header),
                LineStart = symbol.LineStart,
                LineEnd = symbol.LineStart + 5,
                SectionTitle = $"{symbol.Kind}: {symbol.Name}",
                Metadata =
                {
                    ["symbolName"] = symbol.Name,
                    ["symbolKind"] = symbol.Kind.ToString(),
                    ["isHeader"] = "true"
                }
            });

            // Emit chunks for each child symbol
            foreach (var child in symbol.Children)
            {
                ChunkSymbol(child, document, options, chunks);
            }
        }
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
