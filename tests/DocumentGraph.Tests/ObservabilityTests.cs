using DocumentGraph.Core.Observability;
using Xunit;

namespace DocumentGraph.Tests;

public class ObservabilityTests
{
    [Fact]
    public void ObservabilityService_RecordIndexing_UpdatesSnapshotAccurately()
    {
        var obs = new ObservabilityService();
        obs.RecordIndexing(
            scanned: 10,
            indexed: 8,
            skipped: 2,
            failed: 0,
            chunksCreated: 42,
            embeddingsGenerated: 42,
            duration: TimeSpan.FromSeconds(3.5));

        var snapshot = obs.GetSnapshot();

        Assert.Equal(10, snapshot.FilesScanned);
        Assert.Equal(8, snapshot.FilesIndexed);
        Assert.Equal(2, snapshot.FilesSkipped);
        Assert.Equal(0, snapshot.FilesFailed);
        Assert.Equal(42, snapshot.ChunksCreated);
        Assert.Equal(42, snapshot.EmbeddingsGenerated);
        Assert.Equal(3.5, snapshot.LastIndexDurationSeconds);
        Assert.NotNull(snapshot.LastIndexAt);
    }

    [Fact]
    public void ObservabilityService_RecordSearch_ComputesAverageLatencyAndModes()
    {
        var obs = new ObservabilityService();
        obs.RecordSearch("hybrid", TimeSpan.FromMilliseconds(50));
        obs.RecordSearch("hybrid", TimeSpan.FromMilliseconds(150));
        obs.RecordSearch("fts", TimeSpan.FromMilliseconds(100));

        var snapshot = obs.GetSnapshot();

        Assert.Equal(3, snapshot.TotalSearches);
        Assert.Equal(100.0, snapshot.AverageSearchLatencyMs);
        Assert.Equal(2, snapshot.SearchesByMode["hybrid"]);
        Assert.Equal(1, snapshot.SearchesByMode["fts"]);
    }

    [Fact]
    public void ObservabilityService_RecordMcpCall_TracksInvocationsFailuresAndLatency()
    {
        var obs = new ObservabilityService();
        obs.RecordMcpCall("search", TimeSpan.FromMilliseconds(20), success: true);
        obs.RecordMcpCall("search", TimeSpan.FromMilliseconds(40), success: true);
        obs.RecordMcpCall("open", TimeSpan.FromMilliseconds(10), success: false);

        var snapshot = obs.GetSnapshot();

        Assert.True(snapshot.McpTools.ContainsKey("search"));
        var searchTool = snapshot.McpTools["search"];
        Assert.Equal(2, searchTool.Invocations);
        Assert.Equal(0, searchTool.Failures);
        Assert.Equal(30.0, searchTool.AverageLatencyMs);

        Assert.True(snapshot.McpTools.ContainsKey("open"));
        var openTool = snapshot.McpTools["open"];
        Assert.Equal(1, openTool.Invocations);
        Assert.Equal(1, openTool.Failures);
        Assert.Equal(10.0, openTool.AverageLatencyMs);
    }
}
