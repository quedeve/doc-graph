using System.Collections.Concurrent;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Scanner;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Indexer.Watcher;

/// <summary>
/// Monitors directories for file changes (create, edit, delete, rename) and triggers debounced reindexing.
/// </summary>
public class DocumentWatcher : IDisposable
{
    private readonly IndexingPipeline _pipeline;
    private readonly DocumentGraphConfig _config;
    private readonly ILogger<DocumentWatcher> _logger;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _debounceTimer;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);
    private bool _isDisposed;

    public DocumentWatcher(
        IndexingPipeline pipeline,
        DocumentGraphConfig config,
        ILogger<DocumentWatcher> logger)
    {
        _pipeline = pipeline;
        _config = config;
        _logger = logger;
        _debounceTimer = new Timer(OnDebounceTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Start watching the specified directory path.
    /// </summary>
    public void Start(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        var watcher = new FileSystemWatcher(fullPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileCreated;
        watcher.Deleted += OnFileDeleted;
        watcher.Renamed += OnFileRenamed;
        watcher.Error += OnWatcherError;

        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);

        _logger.LogInformation("Started watching {Path} for changes", fullPath);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e) => QueueChange(e.FullPath, isDelete: false);
    private void OnFileChanged(object sender, FileSystemEventArgs e) => QueueChange(e.FullPath, isDelete: false);

    private void OnFileDeleted(object sender, FileSystemEventArgs e) => QueueChange(e.FullPath, isDelete: true);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueChange(e.OldFullPath, isDelete: true);
        QueueChange(e.FullPath, isDelete: false);
    }

    private void QueueChange(string path, bool isDelete)
    {
        if (ShouldIgnore(path)) return;

        // Use prefix marker for delete events
        var key = isDelete ? $"DELETE:{path}" : $"UPSERT:{path}";
        _pendingChanges[key] = DateTime.UtcNow;

        // Reset debounce timer
        _debounceTimer.Change(_debounceInterval, Timeout.InfiniteTimeSpan);
    }

    private bool ShouldIgnore(string path)
    {
        var filename = Path.GetFileName(path);
        if (string.IsNullOrEmpty(filename)) return true;

        // Check if any ignored directory name is part of the path
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => _config.Indexing.Ignore.Any(ign => string.Equals(ign, p, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // Temporary or backup files
        if (filename.StartsWith("~$") || filename.StartsWith(".#") || filename.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private async void OnDebounceTick(object? state)
    {
        if (_pendingChanges.IsEmpty) return;

        var snapshot = _pendingChanges.Keys.ToList();
        foreach (var key in snapshot)
        {
            _pendingChanges.TryRemove(key, out _);

            try
            {
                if (key.StartsWith("DELETE:"))
                {
                    var file = key["DELETE:".Length..];
                    _logger.LogInformation("Watcher: File deleted -> {Path}", file);
                    await _pipeline.DeleteFileAsync(file);
                }
                else if (key.StartsWith("UPSERT:"))
                {
                    var file = key["UPSERT:".Length..];
                    if (File.Exists(file))
                    {
                        _logger.LogInformation("Watcher: File changed -> {Path}", file);
                        await _pipeline.IndexSingleFileAsync(file, _config, forceReindex: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Watcher: Error processing change for {Key}", key);
            }
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error encountered");
    }

    public void Stop()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _debounceTimer.Dispose();
    }
}
