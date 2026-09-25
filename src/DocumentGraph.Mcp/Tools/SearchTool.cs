using System.Text.Json;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;

namespace DocumentGraph.Mcp.Tools;

public class SearchTool : IMcpTool
{
    private readonly ISearchService _searchService;

    public SearchTool(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public string Name => "search";

    public string Description =>
        "Search the DocumentGraph index across documents and code using hybrid, semantic vector, or full-text ranking. " +
        "Returns lightweight previews with IDs, snippets, scores, and locations. Follow up with 'open' or 'outline' for full content.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "The search query (keywords, phrase, or natural language question)."
            },
            mode = new
            {
                type = "string",
                description = "Search mode: 'hybrid' (blended FTS and vector search, default), 'text' (full-text keyword search), or 'semantic' (vector similarity).",
                @enum = new[] { "hybrid", "text", "semantic" }
            },
            limit = new
            {
                type = "integer",
                description = "Maximum number of results to return (default 10, max 50)."
            },
            extension = new
            {
                type = "string",
                description = "Optional filter by file extension, e.g. '.pdf' or '.cs'."
            },
            doc_type = new
            {
                type = "string",
                description = "Optional filter by document type: 'code', 'document', 'data', 'text'."
            },
            path_filter = new
            {
                type = "string",
                description = "Optional filter for documents matching a specific directory or file path prefix."
            }
        },
        required = new[] { "query" }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("query", out var queryProp) || string.IsNullOrWhiteSpace(queryProp.GetString()))
        {
            return McpToolCallResult.Text("Error: 'query' parameter is required.", isError: true);
        }

        string queryText = queryProp.GetString()!;
        string mode = "hybrid";
        if (arguments.TryGetProperty("mode", out var modeProp) && !string.IsNullOrWhiteSpace(modeProp.GetString()))
        {
            mode = modeProp.GetString()!.ToLowerInvariant();
        }

        int limit = 10;
        if (arguments.TryGetProperty("limit", out var limitProp) && limitProp.TryGetInt32(out var l) && l > 0)
        {
            limit = Math.Min(l, 50);
        }

        string? pathFilter = null;
        if (arguments.TryGetProperty("path_filter", out var pathProp) && !string.IsNullOrWhiteSpace(pathProp.GetString()))
        {
            pathFilter = pathProp.GetString();
        }

        string? extension = null;
        if (arguments.TryGetProperty("extension", out var extProp) && !string.IsNullOrWhiteSpace(extProp.GetString()))
        {
            var ext = extProp.GetString()!;
            extension = ext.StartsWith('.') ? ext : $".{ext}";
        }

        string? docType = null;
        if (arguments.TryGetProperty("doc_type", out var dtProp) && !string.IsNullOrWhiteSpace(dtProp.GetString()))
        {
            docType = dtProp.GetString();
        }

        var searchQuery = new SearchQuery
        {
            QueryText = queryText,
            Limit = limit,
            Extension = extension,
            DocumentType = docType,
            PathPrefix = pathFilter
        };

        var searchResults = mode switch
        {
            "semantic" => await _searchService.SemanticSearchAsync(searchQuery, cancellationToken),
            "text" => await _searchService.TextSearchAsync(searchQuery, cancellationToken),
            _ => await _searchService.HybridSearchAsync(searchQuery, cancellationToken)
        };

        if (searchResults.Items.Count == 0)
        {
            return McpToolCallResult.Text($"No matches found for '{queryText}' (mode: {mode}).");
        }

        var lightweight = searchResults.Items.Select(r => new
        {
            chunk_id = r.ChunkId,
            document_id = r.DocumentId,
            document_name = r.Filename,
            document_path = r.Path,
            section = r.SectionTitle,
            location = r.Location,
            score = Math.Round(r.Score, 4),
            preview = r.Preview
        }).ToList();

        return McpToolCallResult.Json(new
        {
            query = queryText,
            mode,
            duration_ms = Math.Round(searchResults.Duration.TotalMilliseconds, 1),
            count = lightweight.Count,
            total_count = searchResults.TotalCount,
            results = lightweight
        });
    }
}
