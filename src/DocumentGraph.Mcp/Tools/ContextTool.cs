using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Search.Services;
using DocumentGraph.Storage.PostgreSQL;

namespace DocumentGraph.Mcp.Tools;

public class ContextTool : IMcpTool
{
    private readonly ISearchService _searchService;
    private readonly DocumentGraphDbContext _db;
    private readonly IRelationshipService _relationshipService;

    public ContextTool(
        ISearchService searchService,
        DocumentGraphDbContext db,
        IRelationshipService relationshipService)
    {
        _searchService = searchService;
        _db = db;
        _relationshipService = relationshipService;
    }

    public string Name => "context";

    public string Description =>
        "High-level context assembler for complex queries. Searches, retrieves, deduplicates, and packs the most relevant document chunks and knowledge graph connections into a token-capped context package.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "The topic, question, or feature to gather comprehensive context for."
            },
            max_tokens = new
            {
                type = "integer",
                description = "Maximum estimated token budget for the returned context (default: 4000, max: 16000)."
            },
            mode = new
            {
                type = "string",
                description = "Search mode: 'hybrid' (default), 'text', or 'semantic'.",
                @enum = new[] { "hybrid", "text", "semantic" }
            },
            extension = new
            {
                type = "string",
                description = "Optional file extension filter, e.g. '.pdf' or '.cs'."
            },
            include_relationships = new
            {
                type = "boolean",
                description = "Whether to include 1-hop knowledge graph relationships for key matched entities (default: true)."
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

        string query = queryProp.GetString()!;
        int maxTokens = 4000;
        if (arguments.TryGetProperty("max_tokens", out var mtProp) && mtProp.TryGetInt32(out var mt) && mt > 0)
        {
            maxTokens = Math.Min(mt, 16000);
        }

        string mode = "hybrid";
        if (arguments.TryGetProperty("mode", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString()))
        {
            mode = mProp.GetString()!.ToLowerInvariant();
        }

        string? extension = null;
        if (arguments.TryGetProperty("extension", out var extProp) && !string.IsNullOrWhiteSpace(extProp.GetString()))
        {
            var ext = extProp.GetString()!;
            extension = ext.StartsWith('.') ? ext : $".{ext}";
        }

        bool includeRelationships = true;
        if (arguments.TryGetProperty("include_relationships", out var irProp) && irProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            includeRelationships = irProp.GetBoolean();
        }

        // 1. Search for candidate chunks
        var searchQuery = new SearchQuery
        {
            QueryText = query,
            Limit = 20,
            Extension = extension
        };

        var searchResults = mode switch
        {
            "semantic" => await _searchService.SemanticSearchAsync(searchQuery, cancellationToken),
            "text" => await _searchService.TextSearchAsync(searchQuery, cancellationToken),
            _ => await _searchService.HybridSearchAsync(searchQuery, cancellationToken)
        };

        if (searchResults.Items.Count == 0)
        {
            return McpToolCallResult.Text($"No relevant context found for '{query}'.");
        }

        // 2. Fetch full chunk entities to assemble text
        var chunkIds = searchResults.Items.Select(i => i.ChunkId).Distinct().ToList();
        var chunks = await _db.Chunks
            .Include(c => c.Document)
            .AsNoTracking()
            .Where(c => chunkIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // 3. Collect relationships for top documents/symbols if requested
        var relationshipLines = new List<string>();
        if (includeRelationships)
        {
            var topDocNames = searchResults.Items
                .Select(i => i.Filename)
                .Distinct()
                .Take(3);

            foreach (var docName in topDocNames)
            {
                var rel = await _relationshipService.GetRelatedAsync(docName, cancellationToken);
                if (rel != null)
                {
                    foreach (var edge in rel.Outgoing.Take(3))
                    {
                        relationshipLines.Add($"• [{docName}] --({edge.RelationshipType})--> [{edge.Target.Name}] ({edge.Target.Location})");
                    }
                    foreach (var edge in rel.Incoming.Take(3))
                    {
                        relationshipLines.Add($"• [{edge.Source.Name}] --({edge.RelationshipType})--> [{docName}]");
                    }
                    if (!string.IsNullOrWhiteSpace(rel.CodeGraphContext))
                    {
                        var preview = rel.CodeGraphContext.Length > 300 ? rel.CodeGraphContext[..300] + "..." : rel.CodeGraphContext;
                        relationshipLines.Add($"• [CodeGraph Integration] [{docName}]: {preview.Replace("\n", " ").Trim()}");
                    }
                }
            }
        }

        // 4. Assemble context respecting max_tokens (approx 4 chars per token)
        int maxChars = maxTokens * 4;
        var sb = new StringBuilder();

        sb.AppendLine($"# Context Package: {query}");
        sb.AppendLine($"Mode: {mode} | Search Matches: {searchResults.Items.Count}");
        sb.AppendLine("---");

        if (relationshipLines.Count > 0)
        {
            sb.AppendLine("\n## Knowledge Graph Connections:");
            foreach (var r in relationshipLines.Distinct())
            {
                sb.AppendLine(r);
            }
            sb.AppendLine("---");
        }

        sb.AppendLine("\n## Relevant Excerpts:");

        var includedDocs = new HashSet<string>();
        int includedChunks = 0;
        var seenContentHashes = new HashSet<string>();

        foreach (var item in searchResults.Items)
        {
            if (!chunks.TryGetValue(item.ChunkId, out var chunk))
                continue;

            // Deduplicate near-identical snippets
            string snippetKey = chunk.Content.Length > 80 ? chunk.Content[..80] : chunk.Content;
            if (!seenContentHashes.Add(snippetKey))
                continue;

            var excerptHeader = new StringBuilder();
            excerptHeader.AppendLine($"\n### [{item.Filename}] {item.SectionTitle ?? "Excerpt"}");
            var loc = item.Location ?? (chunk.PageNumber.HasValue ? $"page {chunk.PageNumber}" : $"lines {chunk.LineStart}-{chunk.LineEnd}");
            excerptHeader.AppendLine($"Location: {loc} | Path: {item.Path} | Score: {item.Score:F3}");
            excerptHeader.AppendLine("```");

            string excerptText = chunk.Content.Trim();
            string excerptFooter = "\n```\n";

            int itemLength = excerptHeader.Length + excerptText.Length + excerptFooter.Length;

            if (sb.Length + itemLength > maxChars)
            {
                // Can we fit a truncated version?
                int remainingChars = maxChars - sb.Length - excerptHeader.Length - excerptFooter.Length - 100;
                if (remainingChars > 200)
                {
                    sb.Append(excerptHeader);
                    sb.Append(excerptText[..remainingChars]);
                    sb.AppendLine("\n... [truncated to fit token budget]");
                    sb.Append(excerptFooter);
                    includedChunks++;
                    includedDocs.Add(item.Filename);
                }
                break;
            }

            sb.Append(excerptHeader);
            sb.Append(excerptText);
            sb.Append(excerptFooter);

            includedChunks++;
            includedDocs.Add(item.Filename);
        }

        int estimatedTokens = sb.Length / 4;
        sb.Insert(0, $"<!-- Summary: {includedChunks} chunks from {includedDocs.Count} documents (~{estimatedTokens} tokens) -->\n\n");

        return McpToolCallResult.Text(sb.ToString());
    }
}
