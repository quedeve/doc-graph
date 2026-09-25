using DocumentGraph.Core.Entities;

namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying indexed documents.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Get a document by its file path.
    /// </summary>
    Task<DocumentEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a document by its ID.
    /// </summary>
    Task<DocumentEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or update a document and its associated sections, chunks, and symbols.
    /// If a document with the same path already exists, it is replaced.
    /// </summary>
    Task<DocumentEntity> UpsertAsync(DocumentEntity document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a document and all its associated data by path.
    /// </summary>
    Task<bool> DeleteByPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all indexed document paths and their hashes for incremental indexing.
    /// </summary>
    Task<Dictionary<string, string>> GetAllPathHashesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get index statistics.
    /// </summary>
    Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// List all indexed documents, optionally filtered by document type or extension.
    /// </summary>
    Task<List<DocumentEntity>> ListAsync(
        string? documentType = null,
        string? extension = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find symbols matching a query name.
    /// </summary>
    Task<List<SymbolEntity>> FindSymbolsAsync(
        string name,
        string? symbolType = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chunks that do not yet have vector embeddings.
    /// </summary>
    Task<List<ChunkEntity>> GetChunksWithoutEmbeddingsAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update embeddings for a set of chunks.
    /// </summary>
    Task UpdateChunkEmbeddingsAsync(Dictionary<long, Pgvector.Vector> embeddings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save relationships between documents, sections, chunks, and symbols.
    /// </summary>
    Task AddRelationshipsAsync(IEnumerable<RelationshipEntity> relationships, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all relationships where the item is either source or target.
    /// </summary>
    Task<List<RelationshipEntity>> GetRelationshipsAsync(long itemId, string itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete existing relationships originating from a source item.
    /// </summary>
    Task DeleteRelationshipsForSourceAsync(long sourceId, string sourceType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the current index state.
/// </summary>
public class IndexStatistics
{
    public int TotalDocuments { get; set; }
    public int TotalSections { get; set; }
    public int TotalChunks { get; set; }
    public int TotalSymbols { get; set; }
    public int TotalRelationships { get; set; }
    public int ChunksWithEmbeddings { get; set; }
    public DateTime? LastIndexedAt { get; set; }

    public int FilesScanned { get; set; }
    public int FilesIndexed { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public double IndexDurationSeconds { get; set; }
    public double AverageSearchLatencyMs { get; set; }
    public int TotalSearches { get; set; }

    /// <summary>
    /// Breakdown of documents by type.
    /// </summary>
    public Dictionary<string, int> DocumentsByType { get; set; } = [];

    /// <summary>
    /// Breakdown of documents by extension.
    /// </summary>
    public Dictionary<string, int> DocumentsByExtension { get; set; } = [];
}
