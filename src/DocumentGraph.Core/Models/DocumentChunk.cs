namespace DocumentGraph.Core.Models;

/// <summary>
/// An indexable, searchable unit of content derived from a <see cref="DocumentSection"/>.
/// Chunks are the atomic units stored in the search index and used for retrieval.
/// A section may produce one or more chunks depending on the chunking strategy.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Zero-based index of this chunk within the parent document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Estimated token count for this chunk.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Page number (1-based) if applicable.
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// Slide number (1-based) if applicable.
    /// </summary>
    public int? SlideNumber { get; set; }

    /// <summary>
    /// Worksheet name if applicable.
    /// </summary>
    public string? SheetName { get; set; }

    /// <summary>
    /// Starting line number (1-based) if applicable.
    /// </summary>
    public int? LineStart { get; set; }

    /// <summary>
    /// Ending line number (1-based, inclusive) if applicable.
    /// </summary>
    public int? LineEnd { get; set; }

    /// <summary>
    /// Section title this chunk belongs to — provides context for search results.
    /// </summary>
    public string? SectionTitle { get; set; }

    /// <summary>
    /// The embedding vector. Null until embeddings are generated.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Arbitrary metadata for this chunk.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
