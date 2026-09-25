using System.Collections.Concurrent;

namespace DocumentGraph.Core.Observability;

public interface IObservabilityService
{
    void RecordIndexing(int scanned, int indexed, int skipped, int failed, int chunksCreated, int embeddingsGenerated, TimeSpan duration);
    void RecordSearch(string mode, TimeSpan duration);
    void RecordMcpCall(string toolName, TimeSpan duration, bool success);
    ObservabilitySnapshot GetSnapshot();
}

public class ObservabilityService : IObservabilityService
{
    private readonly object _indexLock = new();
    private int _filesScanned;
    private int _filesIndexed;
    private int _filesSkipped;
    private int _filesFailed;
    private int _chunksCreated;
    private int _embeddingsGenerated;
    private TimeSpan _lastIndexDuration;
    private DateTime? _lastIndexAt;

    private readonly ConcurrentBag<double> _searchLatenciesMs = [];
    private readonly ConcurrentDictionary<string, int> _searchModes = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, int> _mcpToolCalls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _mcpToolFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _mcpToolLatencies = new(StringComparer.OrdinalIgnoreCase);

    public void RecordIndexing(int scanned, int indexed, int skipped, int failed, int chunksCreated, int embeddingsGenerated, TimeSpan duration)
    {
        lock (_indexLock)
        {
            _filesScanned += scanned;
            _filesIndexed += indexed;
            _filesSkipped += skipped;
            _filesFailed += failed;
            _chunksCreated += chunksCreated;
            _embeddingsGenerated += embeddingsGenerated;
            _lastIndexDuration = duration;
            _lastIndexAt = DateTime.UtcNow;
        }
    }

    public void RecordSearch(string mode, TimeSpan duration)
    {
        _searchLatenciesMs.Add(duration.TotalMilliseconds);
        _searchModes.AddOrUpdate(mode, 1, (_, count) => count + 1);
    }

    public void RecordMcpCall(string toolName, TimeSpan duration, bool success)
    {
        _mcpToolCalls.AddOrUpdate(toolName, 1, (_, count) => count + 1);
        if (!success)
        {
            _mcpToolFailures.AddOrUpdate(toolName, 1, (_, count) => count + 1);
        }

        var latencies = _mcpToolLatencies.GetOrAdd(toolName, _ => new ConcurrentBag<double>());
        latencies.Add(duration.TotalMilliseconds);
    }

    public ObservabilitySnapshot GetSnapshot()
    {
        double avgSearchMs = _searchLatenciesMs.IsEmpty ? 0 : _searchLatenciesMs.Average();

        var toolStats = new Dictionary<string, ToolMetrics>();
        foreach (var (tool, count) in _mcpToolCalls)
        {
            _mcpToolFailures.TryGetValue(tool, out int fails);
            double avgMs = 0;
            if (_mcpToolLatencies.TryGetValue(tool, out var lats) && !lats.IsEmpty)
            {
                avgMs = lats.Average();
            }

            toolStats[tool] = new ToolMetrics
            {
                Invocations = count,
                Failures = fails,
                AverageLatencyMs = Math.Round(avgMs, 2)
            };
        }

        lock (_indexLock)
        {
            return new ObservabilitySnapshot
            {
                FilesScanned = _filesScanned,
                FilesIndexed = _filesIndexed,
                FilesSkipped = _filesSkipped,
                FilesFailed = _filesFailed,
                ChunksCreated = _chunksCreated,
                EmbeddingsGenerated = _embeddingsGenerated,
                LastIndexDurationSeconds = Math.Round(_lastIndexDuration.TotalSeconds, 2),
                LastIndexAt = _lastIndexAt,

                TotalSearches = _searchLatenciesMs.Count,
                AverageSearchLatencyMs = Math.Round(avgSearchMs, 2),
                SearchesByMode = new Dictionary<string, int>(_searchModes),

                McpTools = toolStats
            };
        }
    }
}

public class ToolMetrics
{
    public int Invocations { get; set; }
    public int Failures { get; set; }
    public double AverageLatencyMs { get; set; }
}

public class ObservabilitySnapshot
{
    public int FilesScanned { get; set; }
    public int FilesIndexed { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public int ChunksCreated { get; set; }
    public int EmbeddingsGenerated { get; set; }
    public double LastIndexDurationSeconds { get; set; }
    public DateTime? LastIndexAt { get; set; }

    public int TotalSearches { get; set; }
    public double AverageSearchLatencyMs { get; set; }
    public Dictionary<string, int> SearchesByMode { get; set; } = [];

    public Dictionary<string, ToolMetrics> McpTools { get; set; } = [];
}
