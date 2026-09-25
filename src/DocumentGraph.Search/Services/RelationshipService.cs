using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Storage.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Search.Services;

/// <summary>
/// Relationship graph query result.
/// </summary>
public class RelationshipGraphNode
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class RelationshipGraphEdge
{
    public string RelationshipType { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public RelationshipGraphNode Source { get; set; } = new();
    public RelationshipGraphNode Target { get; set; } = new();
}

public class RelatedItemsResult
{
    public RelationshipGraphNode Center { get; set; } = new();
    public List<RelationshipGraphEdge> Incoming { get; set; } = [];
    public List<RelationshipGraphEdge> Outgoing { get; set; } = [];
    public string? CodeGraphContext { get; set; }
}

/// <summary>
/// Service to extract deterministic and cross-reference relationships,
/// and query knowledge graph neighborhoods.
/// </summary>
public interface IRelationshipService
{
    /// <summary>
    /// Builds and persists relationships for a document:
    /// - Structural (Document CONTAINS Section, Section CONTAINS Chunk, Class CONTAINS Method)
    /// - Reference/Mentions (detects mentions of known code symbols and documents across chunk text)
    /// </summary>
    Task<int> BuildRelationshipsForDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds relationships for all currently indexed documents.
    /// </summary>
    Task<int> BuildAllRelationshipsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all incoming and outgoing relationships for a given item (by name, symbol, or filename).
    /// </summary>
    Task<RelatedItemsResult?> GetRelatedAsync(string targetNameOrPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find references to a symbol across all documents.
    /// </summary>
    Task<List<RelationshipGraphEdge>> FindReferencesAsync(string symbolName, CancellationToken cancellationToken = default);
}

public class RelationshipService : IRelationshipService
{
    private readonly DocumentGraphDbContext _db;
    private readonly IDocumentRepository _repository;
    private readonly ILogger<RelationshipService> _logger;
    private readonly Core.CodeGraph.ICodeGraphBridge? _codeGraphBridge;

    public RelationshipService(
        DocumentGraphDbContext db,
        IDocumentRepository repository,
        ILogger<RelationshipService> logger,
        Core.CodeGraph.ICodeGraphBridge? codeGraphBridge = null)
    {
        _db = db;
        _repository = repository;
        _logger = logger;
        _codeGraphBridge = codeGraphBridge;
    }

    public async Task<int> BuildRelationshipsForDocumentAsync(long documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents
            .Include(d => d.Sections)
            .Include(d => d.Chunks)
            .Include(d => d.Symbols)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (doc == null) return 0;

        // Clear previous relationships where this document is source
        await _repository.DeleteRelationshipsForSourceAsync(doc.Id, "Document", cancellationToken);

        var rels = new List<RelationshipEntity>();

        // 1. Structural CONTAINS: Document -> Sections
        foreach (var sec in doc.Sections)
        {
            rels.Add(new RelationshipEntity
            {
                SourceId = doc.Id,
                SourceType = "Document",
                TargetId = sec.Id,
                TargetType = "Section",
                RelationshipType = "CONTAINS",
                Confidence = 1.0f
            });
        }

        // 2. Structural CONTAINS: Document -> Chunks
        foreach (var chunk in doc.Chunks)
        {
            rels.Add(new RelationshipEntity
            {
                SourceId = doc.Id,
                SourceType = "Document",
                TargetId = chunk.Id,
                TargetType = "Chunk",
                RelationshipType = "CONTAINS",
                Confidence = 1.0f
            });

            if (chunk.SectionId.HasValue)
            {
                rels.Add(new RelationshipEntity
                {
                    SourceId = chunk.SectionId.Value,
                    SourceType = "Section",
                    TargetId = chunk.Id,
                    TargetType = "Chunk",
                    RelationshipType = "CONTAINS",
                    Confidence = 1.0f
                });
            }
        }

        // 3. Structural CONTAINS: Document -> Symbols & Symbol -> Child Symbols
        foreach (var sym in doc.Symbols)
        {
            rels.Add(new RelationshipEntity
            {
                SourceId = doc.Id,
                SourceType = "Document",
                TargetId = sym.Id,
                TargetType = "Symbol",
                RelationshipType = "DEFINED_IN",
                Confidence = 1.0f
            });

            if (sym.ParentSymbolId.HasValue)
            {
                rels.Add(new RelationshipEntity
                {
                    SourceId = sym.ParentSymbolId.Value,
                    SourceType = "Symbol",
                    TargetId = sym.Id,
                    TargetType = "Symbol",
                    RelationshipType = "CONTAINS",
                    Confidence = 1.0f
                });
            }
        }

        // 4. Cross-Reference MENTIONS: Chunks mentioning symbols declared elsewhere
        // Get all top-level symbols in other documents
        var otherSymbols = await _db.Symbols
            .AsNoTracking()
            .Where(s => s.DocumentId != doc.Id && s.Name.Length >= 4)
            .Select(s => new { s.Id, s.Name, s.SymbolType, s.DocumentId })
            .Take(500)
            .ToListAsync(cancellationToken);

        if (otherSymbols.Count > 0)
        {
            foreach (var chunk in doc.Chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk.Content)) continue;

                foreach (var sym in otherSymbols)
                {
                    if (chunk.Content.Contains(sym.Name, StringComparison.Ordinal))
                    {
                        rels.Add(new RelationshipEntity
                        {
                            SourceId = chunk.Id,
                            SourceType = "Chunk",
                            TargetId = sym.Id,
                            TargetType = "Symbol",
                            RelationshipType = doc.DocumentType == "code" ? "REFERENCES" : "MENTIONS",
                            Confidence = 0.85f,
                            Metadata = new()
                            {
                                ["sourceDoc"] = doc.Filename,
                                ["symbolName"] = sym.Name
                            }
                        });
                    }
                }
            }
        }

        if (rels.Count > 0)
        {
            await _repository.AddRelationshipsAsync(rels, cancellationToken);
        }

        return rels.Count;
    }

    public async Task<int> BuildAllRelationshipsAsync(CancellationToken cancellationToken = default)
    {
        var docIds = await _db.Documents
            .AsNoTracking()
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        int total = 0;
        foreach (var id in docIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += await BuildRelationshipsForDocumentAsync(id, cancellationToken);
        }

        return total;
    }

    public async Task<RelatedItemsResult?> GetRelatedAsync(string targetNameOrPath, CancellationToken cancellationToken = default)
    {
        RelatedItemsResult? result = null;

        // Try finding as symbol
        var sym = await _db.Symbols
            .Include(s => s.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => EF.Functions.ILike(s.Name, $"%{targetNameOrPath}%"), cancellationToken);

        if (sym != null)
        {
            result = await GetNodeRelationshipsAsync(
                new RelationshipGraphNode
                {
                    Id = sym.Id,
                    Type = "Symbol",
                    Name = $"{sym.SymbolType} {sym.Name}",
                    Location = $"{sym.Document?.Filename} (lines {sym.LineStart}-{sym.LineEnd})"
                }, cancellationToken);
        }
        else
        {
            // Try finding as document
            var doc = await _db.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Filename, $"%{targetNameOrPath}%") ||
                                          EF.Functions.ILike(d.Path, $"%{targetNameOrPath}%"), cancellationToken);

            if (doc != null)
            {
                result = await GetNodeRelationshipsAsync(
                    new RelationshipGraphNode
                    {
                        Id = doc.Id,
                        Type = "Document",
                        Name = doc.Filename,
                        Location = doc.Path
                    }, cancellationToken);
            }
        }

        // Bridge with CodeGraph if available to enrich with code call-graph & symbols
        if (_codeGraphBridge != null && _codeGraphBridge.IsCodeGraphAvailable())
        {
            try
            {
                var cgResult = await _codeGraphBridge.ExploreAsync(targetNameOrPath, cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(cgResult.Output))
                {
                    result ??= new RelatedItemsResult
                    {
                        Center = new RelationshipGraphNode
                        {
                            Id = 0,
                            Type = "CodeSymbol",
                            Name = targetNameOrPath,
                            Location = "CodeGraph"
                        }
                    };

                    result.CodeGraphContext = cgResult.Output;

                    foreach (var s in cgResult.Symbols)
                    {
                        result.Outgoing.Add(new RelationshipGraphEdge
                        {
                            RelationshipType = "CALLS",
                            Confidence = 0.9f,
                            Source = result.Center,
                            Target = new RelationshipGraphNode
                            {
                                Id = 0,
                                Type = "CodeSymbol",
                                Name = $"{s.SymbolType} {s.Name}",
                                Location = s.FilePath
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeGraph bridge exploration failed for '{Target}'", targetNameOrPath);
            }
        }

        return result;
    }

    public async Task<List<RelationshipGraphEdge>> FindReferencesAsync(string symbolName, CancellationToken cancellationToken = default)
    {
        var symbols = await _db.Symbols
            .Where(s => EF.Functions.ILike(s.Name, $"%{symbolName}%"))
            .ToListAsync(cancellationToken);

        if (symbols.Count == 0) return [];

        var symIds = symbols.Select(s => s.Id).ToList();

        var rels = await _db.Relationships
            .AsNoTracking()
            .Where(r => r.TargetType == "Symbol" && symIds.Contains(r.TargetId) &&
                        (r.RelationshipType == "REFERENCES" || r.RelationshipType == "MENTIONS" || r.RelationshipType == "CALLS"))
            .ToListAsync(cancellationToken);

        var edges = new List<RelationshipGraphEdge>();
        foreach (var r in rels)
        {
            var targetSym = symbols.First(s => s.Id == r.TargetId);
            var edge = new RelationshipGraphEdge
            {
                RelationshipType = r.RelationshipType,
                Confidence = r.Confidence,
                Target = new RelationshipGraphNode
                {
                    Id = targetSym.Id,
                    Type = "Symbol",
                    Name = targetSym.Name,
                    Location = targetSym.SymbolType
                }
            };

            // Resolve source
            if (r.SourceType == "Chunk")
            {
                var chunk = await _db.Chunks.Include(c => c.Document).FirstOrDefaultAsync(c => c.Id == r.SourceId, cancellationToken);
                edge.Source = new RelationshipGraphNode
                {
                    Id = r.SourceId,
                    Type = "Chunk",
                    Name = chunk?.Document?.Filename ?? "Document Chunk",
                    Location = chunk?.SectionTitle ?? $"Chunk #{chunk?.ChunkIndex}"
                };
            }
            else if (r.SourceType == "Document")
            {
                var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == r.SourceId, cancellationToken);
                edge.Source = new RelationshipGraphNode
                {
                    Id = r.SourceId,
                    Type = "Document",
                    Name = d?.Filename ?? "Document",
                    Location = d?.Path ?? ""
                };
            }

            edges.Add(edge);
        }

        if (_codeGraphBridge != null && _codeGraphBridge.IsCodeGraphAvailable())
        {
            try
            {
                var cgResult = await _codeGraphBridge.ExploreAsync(symbolName, cancellationToken: cancellationToken);
                foreach (var sym in cgResult.Symbols)
                {
                    edges.Add(new RelationshipGraphEdge
                    {
                        RelationshipType = "REFERENCES",
                        Confidence = 0.9f,
                        Source = new RelationshipGraphNode
                        {
                            Id = 0,
                            Type = "CodeSymbol",
                            Name = $"{sym.SymbolType} {sym.Name}",
                            Location = sym.FilePath
                        },
                        Target = new RelationshipGraphNode
                        {
                            Id = 0,
                            Type = "Symbol",
                            Name = symbolName,
                            Location = "CodeGraph"
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeGraph explore failed during FindReferencesAsync for '{Symbol}'", symbolName);
            }
        }

        return edges;
    }

    private async Task<RelatedItemsResult> GetNodeRelationshipsAsync(RelationshipGraphNode node, CancellationToken cancellationToken)
    {
        var result = new RelatedItemsResult { Center = node };

        var allRels = await _repository.GetRelationshipsAsync(node.Id, node.Type, cancellationToken);

        foreach (var r in allRels)
        {
            if (r.SourceId == node.Id && r.SourceType == node.Type)
            {
                // Outgoing
                result.Outgoing.Add(new RelationshipGraphEdge
                {
                    RelationshipType = r.RelationshipType,
                    Confidence = r.Confidence,
                    Source = node,
                    Target = new RelationshipGraphNode
                    {
                        Id = r.TargetId,
                        Type = r.TargetType,
                        Name = $"{r.TargetType} #{r.TargetId}"
                    }
                });
            }
            else
            {
                // Incoming
                result.Incoming.Add(new RelationshipGraphEdge
                {
                    RelationshipType = r.RelationshipType,
                    Confidence = r.Confidence,
                    Source = new RelationshipGraphNode
                    {
                        Id = r.SourceId,
                        Type = r.SourceType,
                        Name = $"{r.SourceType} #{r.SourceId}"
                    },
                    Target = node
                });
            }
        }

        return result;
    }
}
