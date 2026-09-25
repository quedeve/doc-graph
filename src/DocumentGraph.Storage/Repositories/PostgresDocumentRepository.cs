using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Storage.Repositories;

/// <summary>
/// PostgreSQL implementation of <see cref="IDocumentRepository"/>.
/// </summary>
public class PostgresDocumentRepository : IDocumentRepository
{
    private readonly PostgreSQL.DocumentGraphDbContext _db;
    private readonly ILogger<PostgresDocumentRepository> _logger;
    private readonly Core.Observability.IObservabilityService? _observability;

    public PostgresDocumentRepository(
        PostgreSQL.DocumentGraphDbContext db,
        ILogger<PostgresDocumentRepository> logger,
        Core.Observability.IObservabilityService? observability = null)
    {
        _db = db;
        _logger = logger;
        _observability = observability;
    }

    public async Task<DocumentEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return await _db.Documents
            .Include(d => d.Sections)
            .Include(d => d.Chunks)
            .Include(d => d.Symbols)
            .FirstOrDefaultAsync(d => d.Path == path, cancellationToken);
    }

    public async Task<DocumentEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _db.Documents
            .Include(d => d.Sections)
            .Include(d => d.Chunks)
            .Include(d => d.Symbols)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<DocumentEntity> UpsertAsync(DocumentEntity document, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Documents
            .FirstOrDefaultAsync(d => d.Path == document.Path, cancellationToken);

        if (existing != null)
        {
            // Delete old data (cascade will handle sections/chunks/symbols)
            _db.Documents.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        // Update tsvector for FTS after insert
        await UpdateSearchVectorsAsync(document.Id, cancellationToken);

        return document;
    }

    public async Task<bool> DeleteByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Path == path, cancellationToken);
        if (document == null)
            return false;

        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Dictionary<string, string>> GetAllPathHashesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Documents
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Path, d => d.Hash, cancellationToken);
    }

    public async Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new IndexStatistics
        {
            TotalDocuments = await _db.Documents.CountAsync(cancellationToken),
            TotalSections = await _db.Sections.CountAsync(cancellationToken),
            TotalChunks = await _db.Chunks.CountAsync(cancellationToken),
            TotalSymbols = await _db.Symbols.CountAsync(cancellationToken),
            TotalRelationships = await _db.Relationships.CountAsync(cancellationToken),
            ChunksWithEmbeddings = await _db.Chunks.CountAsync(c => c.Embedding != null, cancellationToken),
            LastIndexedAt = await _db.Documents.MaxAsync(d => (DateTime?)d.IndexedAt, cancellationToken)
        };

        stats.DocumentsByType = await _db.Documents
            .AsNoTracking()
            .GroupBy(d => d.DocumentType)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        stats.DocumentsByExtension = await _db.Documents
            .AsNoTracking()
            .GroupBy(d => d.Extension)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        if (_observability != null)
        {
            var snap = _observability.GetSnapshot();
            stats.FilesScanned = snap.FilesScanned;
            stats.FilesIndexed = snap.FilesIndexed > 0 ? snap.FilesIndexed : stats.TotalDocuments;
            stats.FilesSkipped = snap.FilesSkipped;
            stats.FilesFailed = snap.FilesFailed;
            stats.IndexDurationSeconds = snap.LastIndexDurationSeconds;
            stats.AverageSearchLatencyMs = snap.AverageSearchLatencyMs;
            stats.TotalSearches = snap.TotalSearches;
            if (snap.LastIndexAt.HasValue && (!stats.LastIndexedAt.HasValue || snap.LastIndexAt > stats.LastIndexedAt))
            {
                stats.LastIndexedAt = snap.LastIndexAt;
            }
        }
        else
        {
            stats.FilesIndexed = stats.TotalDocuments;
            stats.FilesScanned = stats.TotalDocuments;
        }

        return stats;
    }

    public async Task<List<DocumentEntity>> ListAsync(
        string? documentType = null,
        string? extension = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Documents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(documentType))
            query = query.Where(d => d.DocumentType == documentType);

        if (!string.IsNullOrEmpty(extension))
            query = query.Where(d => d.Extension == extension);

        return await query
            .OrderBy(d => d.Filename)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SymbolEntity>> FindSymbolsAsync(
        string name,
        string? symbolType = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Symbols
            .Include(s => s.Document)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(symbolType))
            query = query.Where(s => s.SymbolType == symbolType);

        return await query
            .Where(s => EF.Functions.ILike(s.Name, $"%{name}%"))
            .OrderBy(s => s.Name.Length)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChunkEntity>> GetChunksWithoutEmbeddingsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _db.Chunks
            .Where(c => c.Embedding == null && !string.IsNullOrWhiteSpace(c.Content))
            .OrderBy(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateChunkEmbeddingsAsync(Dictionary<long, Pgvector.Vector> embeddings, CancellationToken cancellationToken = default)
    {
        if (embeddings.Count == 0) return;

        var ids = embeddings.Keys.ToList();
        var chunks = await _db.Chunks
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            if (embeddings.TryGetValue(chunk.Id, out var vector))
            {
                chunk.Embedding = vector;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRelationshipsAsync(IEnumerable<RelationshipEntity> relationships, CancellationToken cancellationToken = default)
    {
        _db.Relationships.AddRange(relationships);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RelationshipEntity>> GetRelationshipsAsync(long itemId, string itemType, CancellationToken cancellationToken = default)
    {
        return await _db.Relationships
            .AsNoTracking()
            .Where(r => (r.SourceId == itemId && r.SourceType == itemType) ||
                        (r.TargetId == itemId && r.TargetType == itemType))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteRelationshipsForSourceAsync(long sourceId, string sourceType, CancellationToken cancellationToken = default)
    {
        var rels = await _db.Relationships
            .Where(r => r.SourceId == sourceId && r.SourceType == sourceType)
            .ToListAsync(cancellationToken);

        if (rels.Count > 0)
        {
            _db.Relationships.RemoveRange(rels);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Update tsvector search vectors for all chunks belonging to a document.
    /// Uses raw SQL since EF Core doesn't natively support tsvector generation.
    /// </summary>
    private async Task UpdateSearchVectorsAsync(long documentId, CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE document_chunks
               SET search_vector = to_tsvector('english', content)
               WHERE document_id = {documentId}",
            cancellationToken);
    }
}
