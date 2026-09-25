using System.Text.Json;
using DocumentGraph.Mcp.Protocol;

namespace DocumentGraph.Mcp.Tools;

public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }
    Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default);
}
