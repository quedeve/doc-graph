using DocumentGraph.Core.Configuration;

namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Discovers files eligible for indexing by scanning directories,
/// applying ignore patterns, and checking file size limits.
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Scan a directory tree and return all eligible file paths.
    /// Respects ignore patterns, size limits, and extension filters from config.
    /// </summary>
    /// <param name="rootPath">Root directory to scan.</param>
    /// <param name="config">Indexing configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of absolute file paths eligible for indexing.</returns>
    Task<List<ScannedFile>> ScanAsync(
        string rootPath,
        IndexingConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A file discovered during scanning with its basic metadata.
/// </summary>
public class ScannedFile
{
    /// <summary>
    /// Absolute file path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File extension including dot.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}
