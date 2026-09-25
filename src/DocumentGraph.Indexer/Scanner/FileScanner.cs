using System.Security.Cryptography;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Indexer.Scanner;

/// <summary>
/// Scans directories for files eligible for indexing.
/// Applies ignore patterns, file size limits, and extension filters.
/// </summary>
public class FileScanner : IFileScanner
{
    /// <summary>
    /// All file extensions supported by DocumentGraph parsers.
    /// </summary>
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        ".txt", ".md",
        ".pdf",
        ".docx", ".doc",

        // Office
        ".xlsx", ".xls",
        ".pptx", ".ppt",

        // Data
        ".csv",

        // Code
        ".cs",
        ".ts", ".js",
        ".cshtml",
        ".sql",
        ".json", ".xml"
    };

    private readonly ILogger<FileScanner> _logger;

    public FileScanner(ILogger<FileScanner> logger)
    {
        _logger = logger;
    }

    public Task<List<ScannedFile>> ScanAsync(
        string rootPath,
        IndexingConfig config,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            _logger.LogWarning("Directory does not exist: {Path}", root);
            return Task.FromResult(new List<ScannedFile>());
        }

        var allowedExtensions = config.IncludeExtensions.Count > 0
            ? new HashSet<string>(config.IncludeExtensions, StringComparer.OrdinalIgnoreCase)
            : SupportedExtensions;

        var excludedExtensions = new HashSet<string>(config.ExcludeExtensions, StringComparer.OrdinalIgnoreCase);
        var ignorePatterns = config.Ignore;

        var results = new List<ScannedFile>();
        ScanDirectory(root, root, allowedExtensions, excludedExtensions, ignorePatterns, config.MaxFileSizeBytes, results, cancellationToken);

        _logger.LogInformation("Scanned {Count} eligible files in {Root}", results.Count, root);
        return Task.FromResult(results);
    }

    private void ScanDirectory(
        string currentDir,
        string rootDir,
        HashSet<string> allowedExtensions,
        HashSet<string> excludedExtensions,
        List<string> ignorePatterns,
        long maxFileSize,
        List<ScannedFile> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if this directory should be ignored
        var dirName = Path.GetFileName(currentDir);
        if (ignorePatterns.Any(p => MatchesPattern(dirName, p)))
        {
            _logger.LogDebug("Ignoring directory: {Dir}", currentDir);
            return;
        }

        try
        {
            // Process files in the current directory
            foreach (var filePath in Directory.EnumerateFiles(currentDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension))
                    continue;

                if (!allowedExtensions.Contains(extension))
                    continue;

                if (excludedExtensions.Contains(extension))
                    continue;

                var validation = DocumentGraph.Core.Security.PathGuard.ValidatePath(filePath, new SecurityConfig { EnforceAllowedRoots = false });
                if (!validation.IsValid)
                {
                    _logger.LogDebug("Skipping restricted file {Path}: {Reason}", filePath, validation.ErrorMessage);
                    continue;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    if (fileInfo.Length > maxFileSize)
                    {
                        _logger.LogDebug("Skipping oversized file ({Size} bytes): {Path}", fileInfo.Length, filePath);
                        continue;
                    }

                    results.Add(new ScannedFile
                    {
                        Path = fileInfo.FullName,
                        Extension = extension.ToLowerInvariant(),
                        Size = fileInfo.Length,
                        ModifiedAt = fileInfo.LastWriteTimeUtc
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accessing file: {Path}", filePath);
                }
            }

            // Recurse into subdirectories
            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
                ScanDirectory(subDir, rootDir, allowedExtensions, excludedExtensions, ignorePatterns, maxFileSize, results, cancellationToken);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied: {Dir}", currentDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning directory: {Dir}", currentDir);
        }
    }

    /// <summary>
    /// Simple pattern matching — supports exact name match and leading dot (hidden dirs).
    /// </summary>
    private static bool MatchesPattern(string name, string pattern)
    {
        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compute SHA-256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hashBytes);
    }
}
