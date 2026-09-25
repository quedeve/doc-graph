using System.Text.Json;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;

namespace DocumentGraph.Mcp.Tools;

public class FindTool : IMcpTool
{
    private readonly IDocumentRepository _repository;

    public FindTool(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public string Name => "find";

    public string Description =>
        "Find code symbols (classes, methods, interfaces, functions, enums, records) by name across the indexed codebase. " +
        "Returns symbol locations with line numbers and signatures.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            name = new
            {
                type = "string",
                description = "Name or partial name of the symbol to search for."
            },
            type = new
            {
                type = "string",
                description = "Optional filter by symbol type (e.g., 'Class', 'Method', 'Interface', 'Record', 'Function')."
            },
            limit = new
            {
                type = "integer",
                description = "Maximum results to return (default: 20)."
            }
        },
        required = new[] { "name" }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return McpToolCallResult.Text("Error: 'name' parameter is required.", isError: true);
        }

        string name = nameProp.GetString()!;
        string? type = null;
        if (arguments.TryGetProperty("type", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
        {
            type = typeProp.GetString();
        }

        int limit = 20;
        if (arguments.TryGetProperty("limit", out var limProp) && limProp.TryGetInt32(out var l) && l > 0)
        {
            limit = Math.Min(l, 100);
        }

        var symbols = await _repository.FindSymbolsAsync(name, type, limit, cancellationToken);
        if (symbols.Count == 0)
        {
            return McpToolCallResult.Text($"No symbols found matching '{name}'.");
        }

        var results = symbols.Select(s => new
        {
            id = s.Id,
            name = s.Name,
            symbol_type = s.SymbolType,
            signature = s.Signature,
            namespace_name = s.Namespace,
            document_file = s.Document?.Filename ?? "Unknown",
            document_path = s.Document?.Path ?? "",
            line_start = s.LineStart,
            line_end = s.LineEnd
        }).ToList();

        return McpToolCallResult.Json(new
        {
            query = name,
            type_filter = type,
            count = results.Count,
            symbols = results
        });
    }
}
