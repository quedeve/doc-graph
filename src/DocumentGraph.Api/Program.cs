using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Indexer.Chunking;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Scanner;
using DocumentGraph.Parsers.Code;
using DocumentGraph.Parsers.Data;
using DocumentGraph.Parsers.Documents;
using DocumentGraph.Parsers.Text;
using DocumentGraph.Search.Embeddings;
using DocumentGraph.Search.Services;
using DocumentGraph.Storage.PostgreSQL;
using DocumentGraph.Storage.Repositories;

namespace DocumentGraph.Api;

public class Program
{
    private const string ConfigFileName = "docgraph.json";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Load DocumentGraph configuration
        var config = await LoadConfigAsync();

        // Database
        builder.Services.AddDbContext<DocumentGraphDbContext>(options =>
            options.UseNpgsql(config.Database.ConnectionString, npgsql => npgsql.UseVector()),
            ServiceLifetime.Scoped);

        // Embedding Provider
        if (string.Equals(config.Embedding.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>();
                return new OllamaEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }
        else if (string.Equals(config.Embedding.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<OpenAiCompatibleEmbeddingProvider>>();
                return new OpenAiCompatibleEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }

        // Parsers
        builder.Services.AddSingleton<IDocumentParser, TextParser>();
        builder.Services.AddSingleton<IDocumentParser, MarkdownParser>();
        builder.Services.AddSingleton<IDocumentParser, CsvParser>();
        builder.Services.AddSingleton<IDocumentParser, JsonParser>();
        builder.Services.AddSingleton<IDocumentParser, XmlParser>();
        builder.Services.AddSingleton<IDocumentParser, PdfParser>();
        builder.Services.AddSingleton<IDocumentParser, DocxParser>();
        builder.Services.AddSingleton<IDocumentParser, XlsxParser>();
        builder.Services.AddSingleton<IDocumentParser, PptxParser>();
        builder.Services.AddSingleton<IDocumentParser, CSharpParser>();
        builder.Services.AddSingleton<IDocumentParser, TypeScriptJsParser>();
        builder.Services.AddSingleton<IDocumentParser, SqlParser>();
        builder.Services.AddSingleton<IDocumentParser, CshtmlParser>();

        // Chunkers & Scanner
        builder.Services.AddSingleton<IFileScanner, FileScanner>();
        builder.Services.AddSingleton<IChunker, CodeChunker>();
        builder.Services.AddSingleton<IChunker, DefaultChunker>();

        // Observability & CodeGraph Bridge
        builder.Services.AddSingleton<DocumentGraph.Core.Observability.IObservabilityService, DocumentGraph.Core.Observability.ObservabilityService>();
        builder.Services.AddSingleton<DocumentGraph.Core.CodeGraph.ICodeGraphBridge, DocumentGraph.Search.Services.CodeGraphBridge>();

        // Repository & Pipeline
        builder.Services.AddScoped<IDocumentRepository, PostgresDocumentRepository>();
        builder.Services.AddScoped<IndexingPipeline>();
        builder.Services.AddScoped<IDocumentReader, DocumentReader>();
        builder.Services.AddScoped<IRelationshipService, RelationshipService>();

        // Search Service
        builder.Services.AddScoped<ISearchService>(sp =>
        {
            var db = sp.GetRequiredService<DocumentGraphDbContext>();
            var logger = sp.GetRequiredService<ILogger<PostgresSearchService>>();
            var embeddingProvider = sp.GetService<IEmbeddingProvider>();
            var obs = sp.GetService<DocumentGraph.Core.Observability.IObservabilityService>();
            return new PostgresSearchService(db, logger, embeddingProvider, config.Search, obs);
        });

        var app = builder.Build();

        // --- REST API Endpoints (Section 6 of Plan) ---

        // GET /api/status
        app.MapGet("/api/status", async (
            IDocumentRepository repo,
            DocumentGraph.Core.Observability.IObservabilityService obs,
            CancellationToken ct) =>
        {
            var stats = await repo.GetStatisticsAsync(ct);
            var snapshot = obs.GetSnapshot();
            return Results.Ok(new
            {
                Stats = stats,
                Observability = snapshot
            });
        });

        // POST /api/search
        app.MapPost("/api/search", async ([FromBody] SearchRequest req, ISearchService searchService, CancellationToken ct) =>
        {
            var query = new SearchQuery
            {
                QueryText = req.Query,
                Limit = req.Limit ?? 10,
                DocumentType = req.DocumentType,
                Extension = req.Extension,
                PathPrefix = req.PathPrefix
            };

            var mode = req.Mode?.ToLowerInvariant() ?? "hybrid";
            var results = mode switch
            {
                "semantic" => await searchService.SemanticSearchAsync(query, ct),
                "text" => await searchService.TextSearchAsync(query, ct),
                _ => await searchService.HybridSearchAsync(query, ct)
            };

            return Results.Ok(results);
        });

        // POST /api/semantic-search
        app.MapPost("/api/semantic-search", async ([FromBody] SearchRequest req, ISearchService searchService, CancellationToken ct) =>
        {
            var query = new SearchQuery
            {
                QueryText = req.Query,
                Limit = req.Limit ?? 10,
                DocumentType = req.DocumentType,
                Extension = req.Extension,
                PathPrefix = req.PathPrefix
            };
            var results = await searchService.SemanticSearchAsync(query, ct);
            return Results.Ok(results);
        });

        // POST /api/hybrid-search
        app.MapPost("/api/hybrid-search", async ([FromBody] SearchRequest req, ISearchService searchService, CancellationToken ct) =>
        {
            var query = new SearchQuery
            {
                QueryText = req.Query,
                Limit = req.Limit ?? 10,
                DocumentType = req.DocumentType,
                Extension = req.Extension,
                PathPrefix = req.PathPrefix
            };
            var results = await searchService.HybridSearchAsync(query, ct);
            return Results.Ok(results);
        });

        // GET /api/documents
        app.MapGet("/api/documents", async (
            IDocumentRepository repo,
            [FromQuery] string? docType,
            [FromQuery] string? ext,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            CancellationToken ct) =>
        {
            var docs = await repo.ListAsync(docType, ext, limit ?? 50, offset ?? 0, ct);
            return Results.Ok(docs.Select(d => new
            {
                d.Id,
                d.Path,
                d.Filename,
                d.Extension,
                d.DocumentType,
                d.Title,
                d.Size,
                d.IndexedAt
            }));
        });

        // GET /api/documents/{id}
        app.MapGet("/api/documents/{id:long}", async (long id, IDocumentRepository repo, CancellationToken ct) =>
        {
            var doc = await repo.GetByIdAsync(id, ct);
            return doc != null ? Results.Ok(doc) : Results.NotFound(new { error = $"Document with ID {id} not found." });
        });

        // GET /api/documents/{id}/outline
        app.MapGet("/api/documents/{id:long}/outline", async (long id, IDocumentReader reader, IDocumentRepository repo, CancellationToken ct) =>
        {
            var doc = await repo.GetByIdAsync(id, ct);
            if (doc == null) return Results.NotFound(new { error = $"Document {id} not found." });

            var outline = await reader.GetOutlineAsync(doc.Path, ct);
            return outline != null ? Results.Ok(outline) : Results.NotFound();
        });

        // POST /api/read
        app.MapPost("/api/read", async ([FromBody] ReadContentRequest req, IDocumentReader reader, CancellationToken ct) =>
        {
            var content = await reader.ReadAsync(req, ct);
            return content != null ? Results.Ok(content) : Results.NotFound(new { error = $"Document '{req.PathOrFilename}' not found." });
        });

        // POST /api/find
        app.MapPost("/api/find", async ([FromBody] FindSymbolRequest req, IDocumentRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Symbol 'name' is required." });

            var symbols = await repo.FindSymbolsAsync(req.Name, req.SymbolType, req.Limit ?? 20, ct);
            return Results.Ok(symbols.Select(s => new
            {
                s.Id,
                s.Name,
                s.SymbolType,
                s.Signature,
                s.Namespace,
                Document = s.Document?.Filename,
                Path = s.Document?.Path,
                s.LineStart,
                s.LineEnd
            }));
        });

        // POST /api/related
        app.MapPost("/api/related", async ([FromBody] RelatedRequest req, IRelationshipService relService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Target))
                return Results.BadRequest(new { error = "'target' parameter is required." });

            var related = await relService.GetRelatedAsync(req.Target, ct);
            return related != null ? Results.Ok(related) : Results.NotFound(new { error = $"Target '{req.Target}' not found." });
        });

        // POST /api/references
        app.MapPost("/api/references", async ([FromBody] ReferencesRequest req, IRelationshipService relService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return Results.BadRequest(new { error = "'symbol' parameter is required." });

            var references = await relService.FindReferencesAsync(req.Symbol, ct);
            return Results.Ok(references);
        });

        // POST /api/index
        app.MapPost("/api/index", async ([FromBody] IndexRequest req, IndexingPipeline pipeline, CancellationToken ct) =>
        {
            var path = string.IsNullOrWhiteSpace(req.Path) ? "." : req.Path;
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                return Results.BadRequest(new { error = $"Path '{path}' does not exist." });
            }

            var result = await pipeline.IndexAsync(path, config, cancellationToken: ct);
            return Results.Ok(result);
        });

        await app.RunAsync();
    }

    private static async Task<DocumentGraphConfig> LoadConfigAsync()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var cfg = JsonSerializer.Deserialize<DocumentGraphConfig>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (cfg != null) return cfg;
            }
            catch
            {
                // Fallback
            }
        }

        return new DocumentGraphConfig();
    }
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Mode { get; set; } = "hybrid";
    public int? Limit { get; set; } = 10;
    public string? DocumentType { get; set; }
    public string? Extension { get; set; }
    public string? PathPrefix { get; set; }
}

public class FindSymbolRequest
{
    public string Name { get; set; } = string.Empty;
    public string? SymbolType { get; set; }
    public int? Limit { get; set; } = 20;
}

public class RelatedRequest
{
    public string Target { get; set; } = string.Empty;
}

public class ReferencesRequest
{
    public string Symbol { get; set; } = string.Empty;
}

public class IndexRequest
{
    public string? Path { get; set; }
}
