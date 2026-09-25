namespace DocumentGraph.Core.Configuration;

/// <summary>
/// Root configuration for DocumentGraph.
/// Loaded from docgraph.json or appsettings.
/// </summary>
public class DocumentGraphConfig
{
    /// <summary>
    /// PostgreSQL database configuration.
    /// </summary>
    public DatabaseConfig Database { get; set; } = new();

    /// <summary>
    /// Embedding provider configuration.
    /// </summary>
    public EmbeddingConfig Embedding { get; set; } = new();

    /// <summary>
    /// File indexing configuration.
    /// </summary>
    public IndexingConfig Indexing { get; set; } = new();

    /// <summary>
    /// Search configuration.
    /// </summary>
    public SearchConfig Search { get; set; } = new();

    /// <summary>
    /// Security, path validation, and content protection configuration.
    /// </summary>
    public SecurityConfig Security { get; set; } = new();
}

public class DatabaseConfig
{
    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "Host=localhost;Database=documentgraph;Username=postgres;Password=postgres";
}

public class EmbeddingConfig
{
    /// <summary>
    /// Embedding provider: "ollama", "openai", "none".
    /// </summary>
    public string Provider { get; set; } = "none";

    /// <summary>
    /// Model name (e.g., "nomic-embed-text", "text-embedding-3-small").
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Embedding vector dimension. Must match the model.
    /// </summary>
    public int Dimension { get; set; } = 768;

    /// <summary>
    /// API endpoint for the embedding provider.
    /// </summary>
    public string? Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// API key if required.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Maximum number of texts to embed in a single batch request.
    /// </summary>
    public int BatchSize { get; set; } = 32;
}

public class IndexingConfig
{
    /// <summary>
    /// Root directories to index. Relative paths are resolved from the config file location.
    /// </summary>
    public List<string> Roots { get; set; } = ["./"];

    /// <summary>
    /// Glob patterns to ignore during scanning.
    /// </summary>
    public List<string> Ignore { get; set; } =
    [
        "node_modules",
        "bin",
        "obj",
        ".git",
        ".vs",
        ".idea",
        "packages",
        "dist",
        "build",
        ".codegraph"
    ];

    /// <summary>
    /// Maximum file size in bytes to index. Default: 50MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Maximum degree of parallelism for file indexing.
    /// </summary>
    public int MaxParallelism { get; set; } = 4;

    /// <summary>
    /// File extensions to include. Empty means include all supported extensions.
    /// </summary>
    public List<string> IncludeExtensions { get; set; } = [];

    /// <summary>
    /// File extensions to explicitly exclude.
    /// </summary>
    public List<string> ExcludeExtensions { get; set; } = [];
}

public class SearchConfig
{
    /// <summary>
    /// Weight for FTS score in hybrid search (0.0 to 1.0).
    /// </summary>
    public float FtsWeight { get; set; } = 0.4f;

    /// <summary>
    /// Weight for semantic score in hybrid search (0.0 to 1.0).
    /// </summary>
    public float SemanticWeight { get; set; } = 0.6f;

    /// <summary>
    /// Default maximum results per search.
    /// </summary>
    public int DefaultLimit { get; set; } = 10;

    /// <summary>
    /// Maximum content preview length in characters.
    /// </summary>
    public int MaxPreviewLength { get; set; } = 300;
}

public class SecurityConfig
{
    /// <summary>
    /// Allowed root directories. Access outside these roots is prohibited.
    /// If empty, defaults to current working directory.
    /// </summary>
    public List<string> AllowedRoots { get; set; } = [];

    /// <summary>
    /// If true, enforces path traversal protection and roots containment.
    /// </summary>
    public bool EnforceAllowedRoots { get; set; } = true;

    /// <summary>
    /// Maximum allowable file size in bytes to read/index. Default: 50MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Extensions that should never be opened, read, or indexed (e.g. credentials, binaries, env files).
    /// </summary>
    public List<string> RestrictedExtensions { get; set; } =
    [
        ".exe", ".dll", ".so", ".dylib", ".bin",
        ".key", ".pem", ".pfx", ".p12", ".env"
    ];

    /// <summary>
    /// Whether to sanitize extracted text content (strip null bytes, dangerous control characters).
    /// </summary>
    public bool SanitizeContent { get; set; } = true;
}
