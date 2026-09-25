using System.Text.Json;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Mcp.Tools;

namespace DocumentGraph.Mcp.Server;

public class McpServer
{
    private readonly Dictionary<string, IMcpTool> _tools;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DocumentGraph.Core.Observability.IObservabilityService? _observability;

    public McpServer(IEnumerable<IMcpTool> tools, DocumentGraph.Core.Observability.IObservabilityService? observability = null)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _observability = observability;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("[McpServer] DocumentGraph MCP server starting on stdio...");

        using var stdin = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await stdin.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                // EOF reached
                break;
            }

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string jsonContent = line;

            // Handle Content-Length header framing if client uses HTTP-style header
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                string lenStr = line["Content-Length:".Length..].Trim();
                if (int.TryParse(lenStr, out int length))
                {
                    // Read until blank line
                    while (true)
                    {
                        var headerLine = await stdin.ReadLineAsync(cancellationToken);
                        if (headerLine == null || string.IsNullOrWhiteSpace(headerLine)) break;
                    }

                    // Read exact content
                    char[] buffer = new char[length];
                    int read = 0;
                    while (read < length)
                    {
                        int chunk = await stdin.ReadAsync(buffer, read, length - read);
                        if (chunk <= 0) break;
                        read += chunk;
                    }
                    jsonContent = new string(buffer, 0, read);
                }
            }

            if (string.IsNullOrWhiteSpace(jsonContent)) continue;

            try
            {
                var response = await ProcessRequestAsync(jsonContent, cancellationToken);
                if (response != null)
                {
                    string resJson = JsonSerializer.Serialize(response, _jsonOptions);
                    await stdout.WriteLineAsync(resJson);
                    await stdout.FlushAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[McpServer] Error processing request: {ex}");
            }
        }

        Console.Error.WriteLine("[McpServer] DocumentGraph MCP server shut down.");
    }

    public async Task<JsonRpcResponse?> ProcessRequestAsync(string json, CancellationToken cancellationToken = default)
    {
        JsonRpcRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.Fail(null, -32700, $"Parse error: {ex.Message}");
        }

        if (req == null) return null;

        // If it's a notification without an id, we process it and return null (no response)
        bool isNotification = !req.Id.HasValue;

        switch (req.Method)
        {
            case "initialize":
                var initResult = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    serverInfo = new
                    {
                        name = "DocumentGraph.Mcp",
                        version = "1.0.0"
                    }
                };
                return JsonRpcResponse.Success(req.Id, initResult);

            case "notifications/initialized":
            case "initialized":
                Console.Error.WriteLine("[McpServer] Client initialized successfully.");
                return null;

            case "ping":
                return JsonRpcResponse.Success(req.Id, new { });

            case "tools/list":
                var toolDefs = _tools.Values.Select(t => new McpToolDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    InputSchema = t.InputSchema
                }).ToList();

                return JsonRpcResponse.Success(req.Id, new { tools = toolDefs });

            case "tools/call":
                if (!req.Params.HasValue)
                {
                    return JsonRpcResponse.Fail(req.Id, -32602, "Invalid params: params object required");
                }

                var paramsObj = req.Params.Value;
                if (!paramsObj.TryGetProperty("name", out var toolNameProp))
                {
                    return JsonRpcResponse.Fail(req.Id, -32602, "Invalid params: 'name' is required");
                }

                string toolName = toolNameProp.GetString() ?? "";
                if (!_tools.TryGetValue(toolName, out var tool))
                {
                    return JsonRpcResponse.Fail(req.Id, -32601, $"Tool '{toolName}' not found");
                }

                JsonElement arguments = default;
                if (paramsObj.TryGetProperty("arguments", out var argsProp))
                {
                    arguments = argsProp;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var result = await tool.ExecuteAsync(arguments, cancellationToken);
                    sw.Stop();
                    Console.Error.WriteLine($"[McpServer] Tool '{toolName}' executed in {sw.ElapsedMilliseconds}ms (isError: {result.IsError})");
                    _observability?.RecordMcpCall(toolName, sw.Elapsed, !result.IsError);
                    return JsonRpcResponse.Success(req.Id, result);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.Error.WriteLine($"[McpServer] Error executing tool '{toolName}' after {sw.ElapsedMilliseconds}ms: {ex}");
                    _observability?.RecordMcpCall(toolName, sw.Elapsed, success: false);
                    return JsonRpcResponse.Success(req.Id, McpToolCallResult.Text($"Error executing {toolName}: {ex.Message}", isError: true));
                }

            default:
                if (isNotification) return null;
                return JsonRpcResponse.Fail(req.Id, -32601, $"Method '{req.Method}' not recognized");
        }
    }
}
