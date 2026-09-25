using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentGraph.Mcp.Protocol;

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    public static JsonRpcResponse Success(JsonElement? id, object result) =>
        new() { Id = id, Result = result };

    public static JsonRpcResponse Fail(JsonElement? id, int code, string message, object? data = null) =>
        new() { Id = id, Error = new JsonRpcError { Code = code, Message = message, Data = data } };
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new();
}

public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class McpToolCallResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    public static McpToolCallResult Text(string text, bool isError = false) =>
        new()
        {
            Content = [new McpContent { Type = "text", Text = text }],
            IsError = isError
        };

    public static McpToolCallResult Json(object obj, bool isError = false) =>
        Text(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }), isError);
}
