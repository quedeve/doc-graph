using System.CommandLine;
using System.Text.Json;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Scanner;
using DocumentGraph.Indexer.Chunking;
using DocumentGraph.Parsers.Documents;
using DocumentGraph.Parsers.Data;
using DocumentGraph.Parsers.Text;
using DocumentGraph.Storage.PostgreSQL;
using DocumentGraph.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Tool;

public class Program
{
    private const string ConfigFileName = "docgraph.json";

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DocumentGraph — local document indexing, search, and knowledge graph tool");

        // docgraph init
        var initCommand = new Command("init", "Initialize DocumentGraph configuration and database");
        initCommand.SetHandler(HandleInit);
        rootCommand.AddCommand(initCommand);

        // docgraph index <path>
        var indexCommand = new Command("index", "Index a directory");
        var pathArg = new Argument<string>("path", () => ".", "Directory to index");
        indexCommand.AddArgument(pathArg);
        indexCommand.SetHandler(HandleIndex, pathArg);
        rootCommand.AddCommand(indexCommand);

        // docgraph find <symbol>
        var findCommand = new Command("find", "Find code symbols by name");
        var nameArg = new Argument<string>("symbol", "Symbol name to look up");
        var symbolTypeOption = new Option<string?>("--type", "Filter by symbol type (Class, Method, etc.)");
        var symbolLimitOption = new Option<int>("--limit", () => 20, "Maximum results");
        findCommand.AddArgument(nameArg);
        findCommand.AddOption(symbolTypeOption);
        findCommand.AddOption(symbolLimitOption);
        findCommand.SetHandler(HandleFind, nameArg, symbolTypeOption, symbolLimitOption);
        rootCommand.AddCommand(findCommand);

        // docgraph status
        var statusCommand = new Command("status", "Show index statistics");
        statusCommand.SetHandler(HandleStatus);
        rootCommand.AddCommand(statusCommand);

        // docgraph search <query>
        var searchCommand = new Command("search", "Search the index (supports text, semantic, or hybrid mode)");
        var queryArg = new Argument<string>("query", "Search query text");
        var limitOption = new Option<int>("--limit", () => 10, "Maximum results");
        var typeOption = new Option<string?>("--type", "Filter by document type (document, code, data, text)");
        var extOption = new Option<string?>("--ext", "Filter by file extension (e.g. .cs, .pdf)");
        var pathOption = new Option<string?>("--path", "Filter by file path prefix");
        var modeOption = new Option<string>("--mode", () => "hybrid", "Search mode: hybrid, text, semantic");
        searchCommand.AddArgument(queryArg);
        searchCommand.AddOption(limitOption);
        searchCommand.AddOption(typeOption);
        searchCommand.AddOption(extOption);
        searchCommand.AddOption(pathOption);
        searchCommand.AddOption(modeOption);
        searchCommand.SetHandler(HandleSearch, queryArg, limitOption, typeOption, extOption, pathOption, modeOption);
        rootCommand.AddCommand(searchCommand);

        // docgraph semantic-search <query>
        var semanticCommand = new Command("semantic-search", "Search the index using semantic vector similarity");
        var semQueryArg = new Argument<string>("query", "Natural language query");
        var semLimitOption = new Option<int>("--limit", () => 10, "Maximum results");
        var semTypeOption = new Option<string?>("--type", "Filter by document type (document, code, data, text)");
        var semExtOption = new Option<string?>("--ext", "Filter by file extension (e.g. .cs, .pdf)");
        var semPathOption = new Option<string?>("--path", "Filter by file path prefix");
        semanticCommand.AddArgument(semQueryArg);
        semanticCommand.AddOption(semLimitOption);
        semanticCommand.AddOption(semTypeOption);
        semanticCommand.AddOption(semExtOption);
        semanticCommand.AddOption(semPathOption);
        semanticCommand.SetHandler(HandleSemanticSearch, semQueryArg, semLimitOption, semTypeOption, semExtOption, semPathOption);
        rootCommand.AddCommand(semanticCommand);

        // docgraph embed
        var embedCommand = new Command("embed", "Generate vector embeddings for un-embedded chunks");
        var embedBatchSizeOption = new Option<int>("--batch-size", () => 32, "Batch size for embedding generation");
        embedCommand.AddOption(embedBatchSizeOption);
        embedCommand.SetHandler(HandleEmbed, embedBatchSizeOption);
        rootCommand.AddCommand(embedCommand);

        // docgraph read <file>
        var readCommand = new Command("read", "Read structured content from a document (page, slide, sheet, symbol, lines)");
        var readFileArg = new Argument<string>("file", "Document path or filename");
        var readPageOption = new Option<int?>("--page", "Read a specific page number");
        var readSlideOption = new Option<int?>("--slide", "Read a specific slide number");
        var readSheetOption = new Option<string?>("--sheet", "Read a specific Excel sheet");
        var readSectionOption = new Option<string?>("--section", "Read a specific section title");
        var readSymbolOption = new Option<string?>("--symbol", "Read a specific code symbol");
        var readStartOption = new Option<int?>("--start", "Starting line number");
        var readEndOption = new Option<int?>("--end", "Ending line number");
        readCommand.AddArgument(readFileArg);
        readCommand.AddOption(readPageOption);
        readCommand.AddOption(readSlideOption);
        readCommand.AddOption(readSheetOption);
        readCommand.AddOption(readSectionOption);
        readCommand.AddOption(readSymbolOption);
        readCommand.AddOption(readStartOption);
        readCommand.AddOption(readEndOption);
        readCommand.SetHandler(HandleRead, readFileArg, readPageOption, readSlideOption, readSheetOption, readSectionOption, readSymbolOption, readStartOption, readEndOption);
        rootCommand.AddCommand(readCommand);

        // docgraph outline <file>
        var outlineCommand = new Command("outline", "Show document structure, sections, and symbols");
        var outlineFileArg = new Argument<string>("file", "Document path or filename");
        outlineCommand.AddArgument(outlineFileArg);
        outlineCommand.SetHandler(HandleOutline, outlineFileArg);
        rootCommand.AddCommand(outlineCommand);

        // docgraph watch <path>
        var watchCommand = new Command("watch", "Watch directory continuously and incrementally index file changes");
        var watchPathArg = new Argument<string>("path", () => ".", "Directory to watch");
        watchCommand.AddArgument(watchPathArg);
        watchCommand.SetHandler(HandleWatch, watchPathArg);
        rootCommand.AddCommand(watchCommand);

        // docgraph reindex <file>
        var reindexCommand = new Command("reindex", "Force reindex a single specific file");
        var reindexFileArg = new Argument<string>("file", "File path to reindex");
        reindexCommand.AddArgument(reindexFileArg);
        reindexCommand.SetHandler(HandleReindex, reindexFileArg);
        rootCommand.AddCommand(reindexCommand);

        // docgraph related <target>
        var relatedCommand = new Command("related", "Find connected documents, sections, or symbols in knowledge graph");
        var relatedTargetArg = new Argument<string>("target", "Symbol name, document filename, or path");
        relatedCommand.AddArgument(relatedTargetArg);
        relatedCommand.SetHandler(HandleRelated, relatedTargetArg);
        rootCommand.AddCommand(relatedCommand);

        // docgraph references <symbol>
        var refsCommand = new Command("references", "Find references and mentions of a symbol across all documents");
        var refsSymbolArg = new Argument<string>("symbol", "Symbol name to look up references for");
        refsCommand.AddArgument(refsSymbolArg);
        refsCommand.SetHandler(HandleReferences, refsSymbolArg);
        rootCommand.AddCommand(refsCommand);

        // docgraph build-relationships
        var buildRelsCommand = new Command("build-relationships", "Extract and store all structural and cross-document relationships");
        buildRelsCommand.SetHandler(HandleBuildRelationships);
        rootCommand.AddCommand(buildRelsCommand);

        // docgraph codegraph-sync
        var cgSyncCommand = new Command("codegraph-sync", "Synchronize CodeGraph index for the workspace");
        cgSyncCommand.SetHandler(HandleCodeGraphSync);
        rootCommand.AddCommand(cgSyncCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleInit()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

        if (File.Exists(configPath))
        {
            Console.WriteLine($"Configuration already exists: {configPath}");
            return;
        }

        // Create default config
        var config = new DocumentGraphConfig();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(configPath, json);
        Console.WriteLine($"Created configuration: {configPath}");

        // Initialize database
        try
        {
            await using var db = CreateDbContext(config);
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not connect to database. Update connection string in {ConfigFileName}.");
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. Edit {ConfigFileName} to configure your database connection");
        Console.WriteLine("  2. Run: docgraph index <directory>");
    }

    private static async Task HandleIndex(string path)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var pipeline = services.GetRequiredService<IndexingPipeline>();
        var fullPath = Path.GetFullPath(path);

        Console.WriteLine($"Indexing: {fullPath}");
        Console.WriteLine();

        var result = await pipeline.IndexAsync(fullPath, config);

        Console.WriteLine();
        Console.WriteLine("Results:");
        Console.WriteLine($"  Scanned:  {result.FilesScanned}");
        Console.WriteLine($"  Indexed:  {result.FilesIndexed}");
        Console.WriteLine($"  Skipped:  {result.FilesSkipped}");
        Console.WriteLine($"  Failed:   {result.FilesFailed}");
        Console.WriteLine($"  Deleted:  {result.FilesDeleted}");
        Console.WriteLine($"  Chunks:   {result.ChunksCreated}");
        Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F1}s");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  {error.Path}: {error.Message}");
            }
            Console.ResetColor();
        }
    }

    private static async Task HandleStatus()
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var repo = services.GetRequiredService<IDocumentRepository>();
        var stats = await repo.GetStatisticsAsync();

        Console.WriteLine("DocumentGraph");
        Console.WriteLine();
        Console.WriteLine("Files:");
        Console.WriteLine($"  Scanned: {stats.FilesScanned:N0}");
        Console.WriteLine($"  Indexed: {stats.FilesIndexed:N0}");
        Console.WriteLine($"  Skipped: {stats.FilesSkipped:N0}");
        Console.WriteLine($"  Failed:  {stats.FilesFailed:N0}");
        Console.WriteLine();
        Console.WriteLine("Chunks:");
        Console.WriteLine($"  Total: {stats.TotalChunks:N0}");
        Console.WriteLine();
        Console.WriteLine("Embeddings:");
        Console.WriteLine($"  Total: {stats.ChunksWithEmbeddings:N0}");
        Console.WriteLine();
        Console.WriteLine("Last index:");
        Console.WriteLine($"  {stats.LastIndexedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "never"}");
        Console.WriteLine();
        Console.WriteLine($"Documents:     {stats.TotalDocuments:N0}");
        Console.WriteLine($"Sections:      {stats.TotalSections:N0}");
        Console.WriteLine($"Symbols:       {stats.TotalSymbols:N0}");
        Console.WriteLine($"Relationships: {stats.TotalRelationships:N0}");

        if (stats.TotalSearches > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Searches:");
            Console.WriteLine($"  Total:           {stats.TotalSearches:N0}");
            Console.WriteLine($"  Average Latency: {stats.AverageSearchLatencyMs:F1}ms");
        }

        if (stats.DocumentsByType.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  By type:");
            foreach (var (type, count) in stats.DocumentsByType.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"    {type,-16} {count}");
            }
        }

        if (stats.DocumentsByExtension.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  By extension:");
            foreach (var (ext, count) in stats.DocumentsByExtension.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"    {ext,-16} {count}");
            }
        }
    }

    private static async Task HandleSearch(string query, int limit, string? type, string? ext, string? path, string mode)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var searchService = services.GetService<ISearchService>();
        if (searchService == null)
        {
            Console.WriteLine("Search service not available. Ensure database is configured.");
            return;
        }

        var searchQuery = new SearchQuery
        {
            QueryText = query,
            Limit = limit,
            DocumentType = type,
            Extension = ext,
            PathPrefix = path
        };

        var normalizedMode = mode.ToLowerInvariant();
        Console.WriteLine($"Searching ({normalizedMode}): \"{query}\"");
        Console.WriteLine();

        SearchResults results;
        if (normalizedMode == "semantic")
        {
            results = await searchService.SemanticSearchAsync(searchQuery);
        }
        else if (normalizedMode == "text" || normalizedMode == "fts")
        {
            results = await searchService.TextSearchAsync(searchQuery);
        }
        else
        {
            results = await searchService.HybridSearchAsync(searchQuery);
        }

        if (results.Items.Count == 0)
        {
            Console.WriteLine("No results found.");
            return;
        }

        Console.WriteLine($"Found {results.TotalCount} results ({results.Duration.TotalMilliseconds:F0}ms):");
        Console.WriteLine();

        foreach (var item in results.Items)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {item.Filename}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(item.Location))
                Console.Write($"  ({item.Location})");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [score: {item.Score:F2}]");
            Console.ResetColor();

            if (item.ScoreComponents.Count > 1)
            {
                var comps = string.Join(", ", item.ScoreComponents.Select(kv => $"{kv.Key}: {kv.Value:F3}"));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({comps})");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (!string.IsNullOrEmpty(item.SectionTitle))
                Console.WriteLine($"    Section: {item.SectionTitle}");

            if (!string.IsNullOrEmpty(item.Preview))
            {
                var preview = item.Preview.Length > 120 ? item.Preview[..117] + "..." : item.Preview;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    {preview}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    private static async Task HandleFind(string symbol, string? type, int limit)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var repo = services.GetRequiredService<IDocumentRepository>();
        Console.WriteLine($"Looking up symbol: \"{symbol}\"");
        Console.WriteLine();

        var symbols = await repo.FindSymbolsAsync(symbol, type, limit);

        if (symbols.Count == 0)
        {
            Console.WriteLine("No symbols found.");
            return;
        }

        Console.WriteLine($"Found {symbols.Count} symbols:");
        Console.WriteLine();

        foreach (var s in symbols)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  [{s.SymbolType}]");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" {s.Name}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(s.Signature))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" — {s.Signature}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"    in {s.Document?.Filename ?? "unknown"}");
            Console.ResetColor();
            Console.WriteLine($" (lines {s.LineStart}-{s.LineEnd})");
            Console.WriteLine();
        }
    }

    private static async Task HandleSemanticSearch(string query, int limit, string? type, string? ext, string? path)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var searchService = services.GetService<ISearchService>();
        if (searchService == null)
        {
            Console.WriteLine("Search service not available. Ensure database is configured.");
            return;
        }

        var searchQuery = new SearchQuery
        {
            QueryText = query,
            Limit = limit,
            DocumentType = type,
            Extension = ext,
            PathPrefix = path
        };

        Console.WriteLine($"Semantic search: \"{query}\"");
        Console.WriteLine();

        try
        {
            var results = await searchService.SemanticSearchAsync(searchQuery);

            if (results.Items.Count == 0)
            {
                Console.WriteLine("No results found.");
                return;
            }

            Console.WriteLine($"Found {results.TotalCount} results ({results.Duration.TotalMilliseconds:F0}ms):");
            Console.WriteLine();

            foreach (var item in results.Items)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  {item.Filename}");
                Console.ResetColor();

                if (!string.IsNullOrEmpty(item.Location))
                    Console.Write($"  ({item.Location})");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"  [similarity: {item.Score:F3}]");
                Console.ResetColor();

                if (!string.IsNullOrEmpty(item.SectionTitle))
                    Console.WriteLine($"    Section: {item.SectionTitle}");

                if (!string.IsNullOrEmpty(item.Preview))
                {
                    var preview = item.Preview.Length > 120 ? item.Preview[..117] + "..." : item.Preview;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    {preview}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Semantic search error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task HandleEmbed(int batchSize)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var embeddingProvider = services.GetService<IEmbeddingProvider>();
        if (embeddingProvider == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No embedding provider configured. Update embedding provider settings in docgraph.json.");
            Console.ResetColor();
            return;
        }

        var repo = services.GetRequiredService<IDocumentRepository>();
        Console.WriteLine($"Generating embeddings using {embeddingProvider.ProviderName} (dimension: {embeddingProvider.Dimension})...");
        Console.WriteLine();

        int totalEmbedded = 0;
        while (true)
        {
            var chunks = await repo.GetChunksWithoutEmbeddingsAsync(batchSize);
            if (chunks.Count == 0) break;

            var texts = chunks.Select(c => c.Content).ToList();
            var vectors = await embeddingProvider.EmbedBatchAsync(texts);

            var dict = new Dictionary<long, Pgvector.Vector>();
            for (int i = 0; i < chunks.Count; i++)
            {
                dict[chunks[i].Id] = new Pgvector.Vector(vectors[i]);
            }

            await repo.UpdateChunkEmbeddingsAsync(dict);
            totalEmbedded += chunks.Count;
            Console.WriteLine($"  Embedded {totalEmbedded} chunks...");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Total chunks embedded: {totalEmbedded}");
    }

    private static async Task HandleRead(
        string file,
        int? page,
        int? slide,
        string? sheet,
        string? section,
        string? symbol,
        int? start,
        int? end)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var reader = services.GetRequiredService<IDocumentReader>();
        var request = new ReadContentRequest
        {
            PathOrFilename = file,
            Page = page,
            Slide = slide,
            Sheet = sheet,
            Section = section,
            Symbol = symbol,
            LineStart = start,
            LineEnd = end
        };

        var result = await reader.ReadAsync(request);
        if (result == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Document not found: \"{file}\"");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(result.Filename);
        Console.ResetColor();
        Console.Write($" — {result.TargetDescription}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($" ({result.EstimatedTokens} tokens, {result.TotalChunks} chunks)");
        Console.ResetColor();
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
        Console.WriteLine(result.Content);
        Console.WriteLine();
        Console.WriteLine(new string('-', 60));
    }

    private static async Task HandleOutline(string file)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var reader = services.GetRequiredService<IDocumentReader>();
        var outline = await reader.GetOutlineAsync(file);

        if (outline == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Document not found: \"{file}\"");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(outline.Filename);
        Console.ResetColor();
        Console.WriteLine($"  Path:  {outline.Path}");
        Console.WriteLine($"  Type:  {outline.DocumentType}");
        Console.WriteLine($"  Title: {outline.Title}");

        if (outline.Sections.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Sections:");
            Console.ResetColor();
            foreach (var s in outline.Sections)
            {
                var indent = new string(' ', Math.Max(2, s.Depth * 2));
                var location = s.PageNumber.HasValue ? $" [page {s.PageNumber}]" :
                               s.SlideNumber.HasValue ? $" [slide {s.SlideNumber}]" :
                               !string.IsNullOrEmpty(s.SheetName) ? $" [sheet: {s.SheetName}]" : "";
                Console.WriteLine($"{indent}• {s.Title}{location}");
            }
        }

        if (outline.Symbols.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Symbols:");
            Console.ResetColor();
            foreach (var sym in outline.Symbols)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"  [{sym.SymbolType}]");
                Console.ResetColor();
                Console.Write($" {sym.Name}");
                if (!string.IsNullOrEmpty(sym.Signature))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" ({sym.Signature})");
                    Console.ResetColor();
                }
                Console.WriteLine($" (lines {sym.LineStart}-{sym.LineEnd})");
            }
        }
    }

    private static async Task HandleWatch(string path)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"Starting live watch on: {fullPath}");
        Console.WriteLine("Press Ctrl+C to exit.");
        Console.WriteLine();

        var pipeline = services.GetRequiredService<IndexingPipeline>();
        var logger = services.GetRequiredService<ILogger<DocumentGraph.Indexer.Watcher.DocumentWatcher>>();

        using var watcher = new DocumentGraph.Indexer.Watcher.DocumentWatcher(pipeline, config, logger);
        watcher.Start(fullPath);

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Stopping watch...");
            tcs.TrySetResult();
        };

        await tcs.Task;
    }

    private static async Task HandleReindex(string file)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var pipeline = services.GetRequiredService<IndexingPipeline>();
        var fullPath = Path.GetFullPath(file);

        Console.WriteLine($"Reindexing file: {fullPath}");
        var success = await pipeline.IndexSingleFileAsync(fullPath, config, forceReindex: true);
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Reindexing completed successfully.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Reindexing failed or file could not be parsed.");
            Console.ResetColor();
        }
    }

    private static async Task HandleRelated(string target)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var relService = services.GetRequiredService<DocumentGraph.Search.Services.IRelationshipService>();
        var result = await relService.GetRelatedAsync(target);

        if (result == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Item not found in graph: \"{target}\"");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Graph Neighborhood: {result.Center.Name}]");
        Console.ResetColor();
        Console.WriteLine($"  Type: {result.Center.Type}");
        Console.WriteLine($"  Location: {result.Center.Location}");
        Console.WriteLine();

        if (result.Outgoing.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Outgoing Connections:");
            Console.ResetColor();
            foreach (var edge in result.Outgoing)
            {
                Console.Write($"  --[{edge.RelationshipType}]--> ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{edge.Target.Name} ({edge.Target.Type})");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        if (result.Incoming.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Incoming Connections:");
            Console.ResetColor();
            foreach (var edge in result.Incoming)
            {
                Console.Write($"  <--[{edge.RelationshipType}]-- ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{edge.Source.Name} ({edge.Source.Type})");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(result.CodeGraphContext))
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("CodeGraph Context:");
            Console.ResetColor();
            Console.WriteLine(result.CodeGraphContext);
            Console.WriteLine();
        }
    }

    private static async Task HandleCodeGraphSync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var cgBridge = new DocumentGraph.Search.Services.CodeGraphBridge(
            loggerFactory.CreateLogger<DocumentGraph.Search.Services.CodeGraphBridge>());

        if (!cgBridge.IsCodeGraphAvailable())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("CodeGraph (.codegraph) not found in the current workspace hierarchy.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("Running CodeGraph sync...");
        var success = await cgBridge.SyncAsync();
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("CodeGraph synchronization completed successfully.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("CodeGraph synchronization failed.");
            Console.ResetColor();
        }
    }

    private static async Task HandleReferences(string symbol)
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var relService = services.GetRequiredService<DocumentGraph.Search.Services.IRelationshipService>();
        var references = await relService.FindReferencesAsync(symbol);

        if (references.Count == 0)
        {
            Console.WriteLine($"No references found for symbol: \"{symbol}\"");
            return;
        }

        Console.WriteLine($"Found {references.Count} references to \"{symbol}\":");
        Console.WriteLine();

        foreach (var edge in references)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {edge.Source.Name}");
            Console.ResetColor();
            Console.Write($" [{edge.RelationshipType}] -> ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(edge.Target.Name);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" ({edge.Source.Location})");
            Console.ResetColor();
        }
    }

    private static async Task HandleBuildRelationships()
    {
        var (config, services) = await BuildServices();
        if (config == null) return;

        var relService = services.GetRequiredService<DocumentGraph.Search.Services.IRelationshipService>();
        Console.WriteLine("Building structural and cross-document relationships...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var count = await relService.BuildAllRelationshipsAsync();
        sw.Stop();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Extracted and saved {count} relationships in {sw.Elapsed.TotalSeconds:F1}s.");
        Console.ResetColor();
    }

    // --- Service Setup ---

    private static async Task<(DocumentGraphConfig? config, ServiceProvider services)> BuildServices()
    {
        var config = await LoadConfig();
        if (config == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Configuration not found. Run 'docgraph init' first.");
            Console.ResetColor();
            return (null, new ServiceCollection().BuildServiceProvider());
        }

        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Database
        services.AddDbContext<DocumentGraphDbContext>(options =>
            options.UseNpgsql(config.Database.ConnectionString, npgsql =>
                npgsql.UseVector()));

        // Embedding Provider
        if (string.Equals(config.Embedding.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DocumentGraph.Search.Embeddings.OllamaEmbeddingProvider>>();
                return new DocumentGraph.Search.Embeddings.OllamaEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }
        else if (string.Equals(config.Embedding.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DocumentGraph.Search.Embeddings.OpenAiCompatibleEmbeddingProvider>>();
                return new DocumentGraph.Search.Embeddings.OpenAiCompatibleEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }

        // Core services
        services.AddSingleton<IFileScanner, FileScanner>();

        // Text & Data parsers
        services.AddSingleton<IDocumentParser, TextParser>();
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, CsvParser>();
        services.AddSingleton<IDocumentParser, JsonParser>();
        services.AddSingleton<IDocumentParser, XmlParser>();

        // Document parsers (PDF, Office)
        services.AddSingleton<IDocumentParser, PdfParser>();
        services.AddSingleton<IDocumentParser, DocxParser>();
        services.AddSingleton<IDocumentParser, XlsxParser>();
        services.AddSingleton<IDocumentParser, PptxParser>();

        // Code parsers (Roslyn C#, TS/JS, SQL, CSHTML)
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.CSharpParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.TypeScriptJsParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.SqlParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.CshtmlParser>();

        // Chunking
        services.AddSingleton<IChunker, CodeChunker>();
        services.AddSingleton<IChunker, DefaultChunker>();

        // Observability & CodeGraph Bridge
        services.AddSingleton<DocumentGraph.Core.Observability.IObservabilityService, DocumentGraph.Core.Observability.ObservabilityService>();
        services.AddSingleton<DocumentGraph.Core.CodeGraph.ICodeGraphBridge, DocumentGraph.Search.Services.CodeGraphBridge>();

        // Repository & Pipeline
        services.AddScoped<IDocumentRepository, PostgresDocumentRepository>();
        services.AddScoped<IndexingPipeline>();

        // Reader Service
        services.AddScoped<IDocumentReader, DocumentGraph.Search.Services.DocumentReader>();

        // Relationship Service
        services.AddScoped<DocumentGraph.Search.Services.IRelationshipService, DocumentGraph.Search.Services.RelationshipService>();

        // Search Service (FTS & Semantic)
        services.AddScoped<ISearchService>(sp =>
        {
            var db = sp.GetRequiredService<DocumentGraphDbContext>();
            var logger = sp.GetRequiredService<ILogger<DocumentGraph.Search.Services.PostgresSearchService>>();
            var embeddingProvider = sp.GetService<IEmbeddingProvider>();
            var obs = sp.GetService<DocumentGraph.Core.Observability.IObservabilityService>();
            return new DocumentGraph.Search.Services.PostgresSearchService(db, logger, embeddingProvider, config.Search, obs);
        });

        var provider = services.BuildServiceProvider();
        return (config, provider);
    }

    private static async Task<DocumentGraphConfig?> LoadConfig()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (!File.Exists(configPath))
            return null;

        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<DocumentGraphConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static DocumentGraphDbContext CreateDbContext(DocumentGraphConfig config)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentGraphDbContext>();
        optionsBuilder.UseNpgsql(config.Database.ConnectionString, npgsql => npgsql.UseVector());
        return new DocumentGraphDbContext(optionsBuilder.Options);
    }
}
