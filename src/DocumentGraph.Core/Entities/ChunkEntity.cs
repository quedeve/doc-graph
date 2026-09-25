namespace DocumentGraph.Core.Entities;

/// <summary>
/// Database entity representing an indexable/searchable chunk of content.
/// This is the atomic unit used for FTS and vector search.
/// </summary>
public class ChunkEntity
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public long? SectionId { get; set; }

    /// <summary>
    /// Zero-based index within the parent document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Estimated token count.
    /// </summary>
    public int TokenCount { get; set; }

    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string? SheetName { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }

    /// <summary>
    /// Section title for context in search results.
    /// </summary>
    public string? SectionTitle { get; set; }

    /// <summary>
    /// PostgreSQL tsvector for full-text search. Managed by the database.
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>
    /// pgvector embedding. Null until embeddings are generated.
    /// </summary>
    public Pgvector.Vector? Embedding { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation properties
    public DocumentEntity Document { get; set; } = null!;
    public SectionEntity? Section { get; set; }
}
