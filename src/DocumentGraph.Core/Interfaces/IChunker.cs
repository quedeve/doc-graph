using DocumentGraph.Core.Models;

namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Splits a <see cref="ParsedDocument"/> into searchable <see cref="DocumentChunk"/>s.
/// Chunking strategy varies by document type (code chunks by symbol, PDF by page/section, etc.).
/// </summary>
public interface IChunker
{
    /// <summary>
    /// Returns true if this chunker handles the given document type.
    /// </summary>
    bool CanChunk(string documentType);

    /// <summary>
    /// Split a parsed document into indexable chunks.
    /// Each chunk retains source context (page, section, line range).
    /// </summary>
    List<DocumentChunk> Chunk(ParsedDocument document, ChunkingOptions? options = null);
}

/// <summary>
/// Configuration for the chunking process.
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Target maximum token count per chunk. Default: 512.
    /// </summary>
    public int MaxTokensPerChunk { get; set; } = 512;

    /// <summary>
    /// Number of tokens to overlap between consecutive chunks for context continuity. Default: 50.
    /// </summary>
    public int OverlapTokens { get; set; } = 50;

    /// <summary>
    /// If true, prefer logical boundaries (headings, methods) over fixed-size splits.
    /// Default: true.
    /// </summary>
    public bool PreferLogicalBoundaries { get; set; } = true;
}
