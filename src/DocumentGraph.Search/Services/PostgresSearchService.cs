using System.Data;
using System.Diagnostics;
using System.Text;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Storage.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DocumentGraph.Search.Services;

/// <summary>
/// Full-text search service powered by PostgreSQL tsvector and ts_rank_cd.
/// Supports filtering by document type, file extension, and path prefix.
/// </summary>
public class PostgresSearchService : ISearchService
{
    private readonly DocumentGraphDbContext _db;
    private readonly ILogger<PostgresSearchService> _logger;

    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly DocumentGraph.Core.Configuration.SearchConfig _config;
    private readonly DocumentGraph.Core.Observability.IObservabilityService? _observability;

    public PostgresSearchService(
        DocumentGraphDbContext db,
        ILogger<PostgresSearchService> logger,
        IEmbeddingProvider? embeddingProvider = null,
        DocumentGraph.Core.Configuration.SearchConfig? config = null,
        DocumentGraph.Core.Observability.IObservabilityService? observability = null)
    {
        _db = db;
        _logger = logger;
        _embeddingProvider = embeddingProvider;
        _config = config ?? new DocumentGraph.Core.Configuration.SearchConfig();
        _observability = observability;
    }

    public async Task<SearchResults> TextSearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new SearchResults();

        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            return results;
        }

        try
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine(@"
                SELECT 
                    c.id AS chunk_id,
                    c.document_id,
                    d.filename,
                    d.path,
                    d.document_type,
                    c.section_title,
                    c.page_number,
                    c.slide_number,
                    c.sheet_name,
                    c.line_start,
                    c.line_end,
                    c.content,
                    ts_rank_cd(c.search_vector, websearch_to_tsquery('english', @query_text)) AS score,
                    ts_headline('english', c.content, websearch_to_tsquery('english', @query_text), 
                                'StartSel=<b>, StopSel=</b>, MaxWords=35, MinWords=15, ShortWord=3') AS headline
                FROM document_chunks c
                JOIN documents d ON c.document_id = d.id
                WHERE c.search_vector @@ websearch_to_tsquery('english', @query_text)");

            var parameters = new List<NpgsqlParameter>
            {
                new("query_text", query.QueryText)
            };

            if (!string.IsNullOrEmpty(query.DocumentType))
            {
                sqlBuilder.AppendLine("  AND d.document_type = @doc_type");
                parameters.Add(new("doc_type", query.DocumentType));
            }

            if (!string.IsNullOrEmpty(query.Extension))
            {
                var ext = query.Extension.StartsWith('.') ? query.Extension : "." + query.Extension;
                sqlBuilder.AppendLine("  AND d.extension = @ext");
                parameters.Add(new("ext", ext));
            }

            if (!string.IsNullOrEmpty(query.PathPrefix))
            {
                sqlBuilder.AppendLine("  AND d.path LIKE @path_prefix");
                parameters.Add(new("path_prefix", query.PathPrefix + "%"));
            }

            sqlBuilder.AppendLine("ORDER BY score DESC");
            sqlBuilder.AppendLine("LIMIT @limit");
            parameters.Add(new("limit", query.Limit > 0 ? query.Limit : 10));

            var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sqlBuilder.ToString();
            foreach (var p in parameters)
            {
                cmd.Parameters.Add(p);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var chunkId = reader.GetInt64(reader.GetOrdinal("chunk_id"));
                var docId = reader.GetInt64(reader.GetOrdinal("document_id"));
                var filename = reader.GetString(reader.GetOrdinal("filename"));
                var path = reader.GetString(reader.GetOrdinal("path"));
                var docType = reader.GetString(reader.GetOrdinal("document_type"));

                var sectionTitle = reader.IsDBNull(reader.GetOrdinal("section_title"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("section_title"));

                int? page = reader.IsDBNull(reader.GetOrdinal("page_number"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("page_number"));

                int? slide = reader.IsDBNull(reader.GetOrdinal("slide_number"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("slide_number"));

                string? sheet = reader.IsDBNull(reader.GetOrdinal("sheet_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("sheet_name"));

                int? lineStart = reader.IsDBNull(reader.GetOrdinal("line_start"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("line_start"));

                int? lineEnd = reader.IsDBNull(reader.GetOrdinal("line_end"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("line_end"));

                var rawContent = reader.GetString(reader.GetOrdinal("content"));
                var headline = reader.IsDBNull(reader.GetOrdinal("headline"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("headline"));

                var score = reader.GetFloat(reader.GetOrdinal("score"));

                var location = FormatLocation(page, slide, sheet, lineStart, lineEnd);
                var preview = !string.IsNullOrWhiteSpace(headline) ? headline : MakeFallbackPreview(rawContent);

                results.Items.Add(new SearchResultItem
                {
                    DocumentId = docId,
                    ChunkId = chunkId,
                    Filename = filename,
                    Path = path,
                    DocumentType = docType,
                    SectionTitle = sectionTitle,
                    Location = location,
                    Preview = preview,
                    Score = score,
                    ScoreComponents = { ["fts_rank"] = score }
                });
            }

            results.TotalCount = results.Items.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute PostgreSQL full-text search for '{Query}'", query.QueryText);
            throw;
        }
        finally
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            _observability?.RecordSearch("fts", sw.Elapsed);
        }

        return results;
    }

    public async Task<SearchResults> SemanticSearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new SearchResults();

        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            return results;
        }

        if (_embeddingProvider == null)
        {
            throw new InvalidOperationException("No embedding provider configured. Please check your docgraph.json embedding configuration.");
        }

        try
        {
            // 1. Generate embedding vector for query
            var queryVector = await _embeddingProvider.EmbedAsync(query.QueryText, cancellationToken);
            var pgVector = new Pgvector.Vector(queryVector);

            // 2. Query pgvector using cosine distance (<=>)
            // Cosine similarity = 1 - (c.embedding <=> @query_vector)
            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine(@"
                SELECT 
                    c.id AS chunk_id,
                    c.document_id,
                    d.filename,
                    d.path,
                    d.document_type,
                    c.section_title,
                    c.page_number,
                    c.slide_number,
                    c.sheet_name,
                    c.line_start,
                    c.line_end,
                    c.content,
                    (1 - (c.embedding <=> @query_vector)) AS score
                FROM document_chunks c
                JOIN documents d ON c.document_id = d.id
                WHERE c.embedding IS NOT NULL");

            var parameters = new List<NpgsqlParameter>
            {
                new("query_vector", pgVector)
            };

            if (!string.IsNullOrEmpty(query.DocumentType))
            {
                sqlBuilder.AppendLine("  AND d.document_type = @doc_type");
                parameters.Add(new("doc_type", query.DocumentType));
            }

            if (!string.IsNullOrEmpty(query.Extension))
            {
                var ext = query.Extension.StartsWith('.') ? query.Extension : "." + query.Extension;
                sqlBuilder.AppendLine("  AND d.extension = @ext");
                parameters.Add(new("ext", ext));
            }

            if (!string.IsNullOrEmpty(query.PathPrefix))
            {
                sqlBuilder.AppendLine("  AND d.path LIKE @path_prefix");
                parameters.Add(new("path_prefix", query.PathPrefix + "%"));
            }

            sqlBuilder.AppendLine("ORDER BY c.embedding <=> @query_vector");
            sqlBuilder.AppendLine("LIMIT @limit");
            parameters.Add(new("limit", query.Limit > 0 ? query.Limit : 10));

            var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sqlBuilder.ToString();
            foreach (var p in parameters)
            {
                cmd.Parameters.Add(p);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var chunkId = reader.GetInt64(reader.GetOrdinal("chunk_id"));
                var docId = reader.GetInt64(reader.GetOrdinal("document_id"));
                var filename = reader.GetString(reader.GetOrdinal("filename"));
                var path = reader.GetString(reader.GetOrdinal("path"));
                var docType = reader.GetString(reader.GetOrdinal("document_type"));

                var sectionTitle = reader.IsDBNull(reader.GetOrdinal("section_title"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("section_title"));

                int? page = reader.IsDBNull(reader.GetOrdinal("page_number"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("page_number"));

                int? slide = reader.IsDBNull(reader.GetOrdinal("slide_number"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("slide_number"));

                string? sheet = reader.IsDBNull(reader.GetOrdinal("sheet_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("sheet_name"));

                int? lineStart = reader.IsDBNull(reader.GetOrdinal("line_start"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("line_start"));

                int? lineEnd = reader.IsDBNull(reader.GetOrdinal("line_end"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("line_end"));

                var rawContent = reader.GetString(reader.GetOrdinal("content"));
                var score = reader.GetFloat(reader.GetOrdinal("score"));

                var location = FormatLocation(page, slide, sheet, lineStart, lineEnd);
                var preview = MakeFallbackPreview(rawContent);

                results.Items.Add(new SearchResultItem
                {
                    DocumentId = docId,
                    ChunkId = chunkId,
                    Filename = filename,
                    Path = path,
                    DocumentType = docType,
                    SectionTitle = sectionTitle,
                    Location = location,
                    Preview = preview,
                    Score = (float)Math.Clamp(score, 0.0, 1.0),
                    ScoreComponents = { ["vector_similarity"] = score }
                });
            }

            results.TotalCount = results.Items.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute semantic search for '{Query}'", query.QueryText);
            throw;
        }
        finally
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            _observability?.RecordSearch("semantic", sw.Elapsed);
        }

        return results;
    }

    public async Task<SearchResults> HybridSearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new SearchResults();

        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            return results;
        }

        // If no embedding provider is available, fall back to pure text search
        if (_embeddingProvider == null)
        {
            return await TextSearchAsync(query, cancellationToken);
        }

        try
        {
            // Execute FTS and Semantic search concurrently
            var candidateLimit = Math.Max(query.Limit * 3, 30);
            var ftsQuery = new SearchQuery
            {
                QueryText = query.QueryText,
                Limit = candidateLimit,
                DocumentType = query.DocumentType,
                Extension = query.Extension,
                PathPrefix = query.PathPrefix
            };
            var semQuery = new SearchQuery
            {
                QueryText = query.QueryText,
                Limit = candidateLimit,
                DocumentType = query.DocumentType,
                Extension = query.Extension,
                PathPrefix = query.PathPrefix
            };

            var ftsTask = TextSearchAsync(ftsQuery, cancellationToken);
            var semTask = SemanticSearchAsync(semQuery, cancellationToken);

            await Task.WhenAll(ftsTask, semTask);

            var ftsResults = await ftsTask;
            var semResults = await semTask;

            // Reciprocal Rank Fusion (RRF) + Linear Normalized Blending
            // RRF formula: Score_rrf = sum( weight / (k + rank) ), k = 60
            const float k = 60f;
            var combinedMap = new Dictionary<long, SearchResultItem>();
            var rrfScores = new Dictionary<long, float>();
            var ftsRankMap = new Dictionary<long, float>();
            var semScoreMap = new Dictionary<long, float>();

            // Process FTS rankings
            for (int rank = 0; rank < ftsResults.Items.Count; rank++)
            {
                var item = ftsResults.Items[rank];
                combinedMap[item.ChunkId] = item;
                ftsRankMap[item.ChunkId] = item.Score;

                var ftsContribution = (_config.FtsWeight) / (k + rank + 1);
                rrfScores[item.ChunkId] = rrfScores.GetValueOrDefault(item.ChunkId) + ftsContribution;
            }

            // Process Semantic rankings
            for (int rank = 0; rank < semResults.Items.Count; rank++)
            {
                var item = semResults.Items[rank];
                if (!combinedMap.ContainsKey(item.ChunkId))
                {
                    combinedMap[item.ChunkId] = item;
                }
                semScoreMap[item.ChunkId] = item.Score;

                var semContribution = (_config.SemanticWeight) / (k + rank + 1);
                rrfScores[item.ChunkId] = rrfScores.GetValueOrDefault(item.ChunkId) + semContribution;
            }

            // Compute final fused items
            var fusedItems = new List<SearchResultItem>();
            foreach (var (chunkId, item) in combinedMap)
            {
                var ftsScore = ftsRankMap.GetValueOrDefault(chunkId, 0f);
                var semScore = semScoreMap.GetValueOrDefault(chunkId, 0f);
                var rrfScore = rrfScores.GetValueOrDefault(chunkId, 0f);

                // Populate granular score components for transparency
                item.Score = rrfScore * 100f; // Scale RRF for readability
                item.ScoreComponents["rrf_score"] = rrfScore;
                item.ScoreComponents["fts_score"] = ftsScore;
                item.ScoreComponents["semantic_score"] = semScore;

                fusedItems.Add(item);
            }

            // Order by fused score descending and take requested limit
            results.Items = fusedItems
                .OrderByDescending(x => x.Score)
                .Take(query.Limit > 0 ? query.Limit : 10)
                .ToList();

            results.TotalCount = results.Items.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute hybrid search for '{Query}'", query.QueryText);
            throw;
        }
        finally
        {
            sw.Stop();
            results.Duration = sw.Elapsed;
            _observability?.RecordSearch("hybrid", sw.Elapsed);
        }

        return results;
    }

    private static string? FormatLocation(int? page, int? slide, string? sheet, int? lineStart, int? lineEnd)
    {
        if (page.HasValue)
            return $"page {page.Value}";
        if (slide.HasValue)
            return $"slide {slide.Value}";
        if (!string.IsNullOrEmpty(sheet))
            return $"sheet {sheet}";
        if (lineStart.HasValue)
            return lineEnd.HasValue && lineEnd.Value > lineStart.Value
                ? $"lines {lineStart.Value}-{lineEnd.Value}"
                : $"line {lineStart.Value}";
        return null;
    }

    private static string MakeFallbackPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var clean = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length > 160 ? clean[..157] + "..." : clean;
    }
}
