using DocumentGraph.Core.CodeGraph;
using DocumentGraph.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentGraph.Tests;

public class CodeGraphBridgeTests
{
    [Fact]
    public void CodeGraphBridge_DetectsCurrentWorkspaceAvailability()
    {
        var bridge = new CodeGraphBridge(NullLogger<CodeGraphBridge>.Instance);
        var isAvailable = bridge.IsCodeGraphAvailable(".");

        // Workspace contains .codegraph directory
        Assert.True(isAvailable);
    }

    [Fact]
    public void CodeGraphBridge_ReturnsFalseForNonExistentDirectory()
    {
        var bridge = new CodeGraphBridge(NullLogger<CodeGraphBridge>.Instance);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var isAvailable = bridge.IsCodeGraphAvailable(tempDir);
            Assert.False(isAvailable);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CodeGraphBridge_ExploreAsync_ReturnsOutputForKnownSymbol()
    {
        var bridge = new CodeGraphBridge(NullLogger<CodeGraphBridge>.Instance);
        var result = await bridge.ExploreAsync("IObservabilityService", ".");

        Assert.True(result.IsAvailable);
        Assert.NotNull(result.Output);
        Assert.NotEmpty(result.Output);
    }
}
