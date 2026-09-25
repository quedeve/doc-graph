using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Indexer.Chunking;
using DocumentGraph.Indexer.Scanner;
using DocumentGraph.Mcp.Server;
using DocumentGraph.Mcp.Tools;
using DocumentGraph.Parsers.Data;
using DocumentGraph.Parsers.Documents;
using DocumentGraph.Parsers.Text;
using DocumentGraph.Search.Embeddings;
using DocumentGraph.Search.Services;
using DocumentGraph.Storage.PostgreSQL;
using DocumentGraph.Storage.Repositories;

namespace DocumentGraph.Mcp;

public class Program
{
    private const string ConfigFileName = "docgraph.json";

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        string? configArg = null;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
            {
                configArg = args[i + 1];
            }
        }

        var config = await LoadConfigAsync(configArg);
        var services = ConfigureServices(config);

        using var scope = services.CreateScope();
        var server = scope.ServiceProvider.GetRequiredService<McpServer>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await server.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DocumentGraph.Mcp] Fatal error: {ex}");
            return 1;
        }
    }

    private static IServiceProvider ConfigureServices(DocumentGraphConfig config)
    {
        var services = new ServiceCollection();

        // Stderr logging only to keep stdout clean for JSON-RPC
        services.AddLogging(builder =>
        {
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // PostgreSQL & pgvector
        services.AddDbContext<DocumentGraphDbContext>(options =>
            options.UseNpgsql(config.Database.ConnectionString, npgsql => npgsql.UseVector()),
            ServiceLifetime.Scoped);

        // Embedding Provider
        if (string.Equals(config.Embedding.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>();
                return new OllamaEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }
        else if (string.Equals(config.Embedding.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<OpenAiCompatibleEmbeddingProvider>>();
                return new OpenAiCompatibleEmbeddingProvider(new HttpClient(), config.Embedding, logger);
            });
        }

        // File parsers
        services.AddSingleton<IDocumentParser, TextParser>();
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, CsvParser>();
        services.AddSingleton<IDocumentParser, JsonParser>();
        services.AddSingleton<IDocumentParser, XmlParser>();
        services.AddSingleton<IDocumentParser, PdfParser>();
        services.AddSingleton<IDocumentParser, DocxParser>();
        services.AddSingleton<IDocumentParser, XlsxParser>();
        services.AddSingleton<IDocumentParser, PptxParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.CSharpParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.TypeScriptJsParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.SqlParser>();
        services.AddSingleton<IDocumentParser, DocumentGraph.Parsers.Code.CshtmlParser>();

        // Chunkers & Scanner
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IChunker, CodeChunker>();
        services.AddSingleton<IChunker, DefaultChunker>();

        // Observability & CodeGraph Bridge
        services.AddSingleton<DocumentGraph.Core.Observability.IObservabilityService, DocumentGraph.Core.Observability.ObservabilityService>();
        services.AddSingleton<DocumentGraph.Core.CodeGraph.ICodeGraphBridge, DocumentGraph.Search.Services.CodeGraphBridge>();

        // Repository & Services
        services.AddScoped<IDocumentRepository, PostgresDocumentRepository>();
        services.AddScoped<IDocumentReader, DocumentReader>();
        services.AddScoped<IRelationshipService, RelationshipService>();

        // Search Service
        services.AddScoped<ISearchService>(sp =>
        {
            var db = sp.GetRequiredService<DocumentGraphDbContext>();
            var logger = sp.GetRequiredService<ILogger<PostgresSearchService>>();
            var embeddingProvider = sp.GetService<IEmbeddingProvider>();
            var obs = sp.GetService<DocumentGraph.Core.Observability.IObservabilityService>();
            return new PostgresSearchService(db, logger, embeddingProvider, config.Search, obs);
        });

        // MCP Tools
        services.AddScoped<IMcpTool, SearchTool>();
        services.AddScoped<IMcpTool, OpenTool>();
        services.AddScoped<IMcpTool, OutlineTool>();
        services.AddScoped<IMcpTool, FindTool>();
        services.AddScoped<IMcpTool, ReferencesTool>();
        services.AddScoped<IMcpTool, RelatedTool>();
        services.AddScoped<IMcpTool, ContextTool>();

        // MCP Server
        services.AddScoped<McpServer>();

        return services.BuildServiceProvider();
    }

    private static async Task<DocumentGraphConfig> LoadConfigAsync(string? explicitPath)
    {
        string? resolvedPath = null;

        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
        {
            resolvedPath = explicitPath;
        }
        else
        {
            var envPath = Environment.GetEnvironmentVariable("DOCGRAPH_CONFIG");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                resolvedPath = envPath;
            }
        }

        if (resolvedPath == null)
        {
            // Search upward from current directory
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (currentDir != null)
            {
                var candidate = Path.Combine(currentDir.FullName, ConfigFileName);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    break;
                }
                currentDir = currentDir.Parent;
            }
        }

        if (resolvedPath == null)
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var homeCandidate = Path.Combine(userHome, ".docgraph", ConfigFileName);
            if (File.Exists(homeCandidate))
            {
                resolvedPath = homeCandidate;
            }
        }

        if (resolvedPath != null && File.Exists(resolvedPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(resolvedPath);
                var loaded = JsonSerializer.Deserialize<DocumentGraphConfig>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (loaded != null)
                {
                    Console.Error.WriteLine($"[DocumentGraph.Mcp] Using config from {resolvedPath}");
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DocumentGraph.Mcp] Warning: Failed to read config from {resolvedPath}: {ex.Message}");
            }
        }

        Console.Error.WriteLine("[DocumentGraph.Mcp] No configuration file found, using defaults.");
        return new DocumentGraphConfig();
    }
}
