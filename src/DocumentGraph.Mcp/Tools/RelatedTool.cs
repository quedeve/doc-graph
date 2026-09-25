using System.Text.Json;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Search.Services;

namespace DocumentGraph.Mcp.Tools;

public class RelatedTool : IMcpTool
{
    private readonly IRelationshipService _relationshipService;

    public RelatedTool(IRelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }

    public string Name => "related";

    public string Description =>
        "Discover connected items in the knowledge graph for a given document, section, or symbol. " +
        "Shows structural hierarchy (CONTAINS) and cross-document references.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            target = new
            {
                type = "string",
                description = "Name, path, or identifier of the document, symbol, or section to inspect."
            }
        },
        required = new[] { "target" }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("target", out var targetProp) || string.IsNullOrWhiteSpace(targetProp.GetString()))
        {
            return McpToolCallResult.Text("Error: 'target' parameter is required.", isError: true);
        }

        string target = targetProp.GetString()!;
        var related = await _relationshipService.GetRelatedAsync(target, cancellationToken);

        if (related == null)
        {
            return McpToolCallResult.Text($"No entity found matching target '{target}'.");
        }

        var incoming = related.Incoming.Select(e => new
        {
            relationship = e.RelationshipType,
            confidence = Math.Round(e.Confidence, 2),
            source = new
            {
                id = e.Source.Id,
                type = e.Source.Type,
                name = e.Source.Name,
                location = e.Source.Location
            }
        }).ToList();

        var outgoing = related.Outgoing.Select(e => new
        {
            relationship = e.RelationshipType,
            confidence = Math.Round(e.Confidence, 2),
            target = new
            {
                id = e.Target.Id,
                type = e.Target.Type,
                name = e.Target.Name,
                location = e.Target.Location
            }
        }).ToList();

        return McpToolCallResult.Json(new
        {
            center = new
            {
                id = related.Center.Id,
                type = related.Center.Type,
                name = related.Center.Name,
                location = related.Center.Location
            },
            incoming_count = incoming.Count,
            outgoing_count = outgoing.Count,
            codegraph_context = related.CodeGraphContext,
            incoming,
            outgoing
        });
    }
}
