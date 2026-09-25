namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Searches the document index using FTS, semantic, or hybrid mode.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Full-text search using PostgreSQL FTS.
    /// </summary>
    Task<SearchResults> TextSearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantic search using pgvector embeddings.
    /// </summary>
    Task<SearchResults> SemanticSearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hybrid search combining FTS and semantic scores.
    /// </summary>
    Task<SearchResults> HybridSearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// A search query with filtering and pagination options.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// The search text.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Maximum results to return. Default: 10.
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Filter by document type (e.g., "code", "document").
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Filter by file extension (e.g., ".pdf").
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Filter by path prefix (e.g., "C:\Projects\TAM").
    /// </summary>
    public string? PathPrefix { get; set; }
}

/// <summary>
/// Search results with scored items.
/// </summary>
public class SearchResults
{
    public List<SearchResultItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// A single search result item with relevance score and source context.
/// </summary>
public class SearchResultItem
{
    public long DocumentId { get; set; }
    public long ChunkId { get; set; }

    /// <summary>
    /// Document filename.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// Document file path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Document type classification.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Location within the document (e.g., "page 14", "sheet Authentication", "line 42-58").
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Section title.
    /// </summary>
    public string? SectionTitle { get; set; }

    /// <summary>
    /// Short preview of matching content.
    /// </summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>
    /// Combined relevance score (0.0 to 1.0).
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Individual score components for debugging/tuning.
    /// </summary>
    public Dictionary<string, float> ScoreComponents { get; set; } = [];
}
