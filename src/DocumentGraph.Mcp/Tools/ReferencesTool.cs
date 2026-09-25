using System.Text.Json;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Search.Services;

namespace DocumentGraph.Mcp.Tools;

public class ReferencesTool : IMcpTool
{
    private readonly IRelationshipService _relationshipService;

    public ReferencesTool(IRelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }

    public string Name => "references";

    public string Description =>
        "Find references, mentions, or calls to a code symbol across all documents, code, and documentation in the index.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            symbol = new
            {
                type = "string",
                description = "Name of the symbol to trace references for."
            }
        },
        required = new[] { "symbol" }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("symbol", out var symProp) || string.IsNullOrWhiteSpace(symProp.GetString()))
        {
            return McpToolCallResult.Text("Error: 'symbol' parameter is required.", isError: true);
        }

        string symbol = symProp.GetString()!;
        var edges = await _relationshipService.FindReferencesAsync(symbol, cancellationToken);

        if (edges.Count == 0)
        {
            return McpToolCallResult.Text($"No references found for symbol '{symbol}'. Tip: ensure 'docgraph build-relationships' has been run.");
        }

        var results = edges.Select(e => new
        {
            relationship = e.RelationshipType,
            confidence = Math.Round(e.Confidence, 2),
            referenced_symbol = e.Target.Name,
            source = new
            {
                type = e.Source.Type,
                name = e.Source.Name,
                location = e.Source.Location,
                id = e.Source.Id
            }
        }).ToList();

        return McpToolCallResult.Json(new
        {
            symbol,
            count = results.Count,
            references = results
        });
    }
}
