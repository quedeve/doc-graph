using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Storage.PostgreSQL;

namespace DocumentGraph.Mcp.Tools;

public class OutlineTool : IMcpTool
{
    private readonly IDocumentReader _reader;
    private readonly DocumentGraphDbContext _dbContext;

    public OutlineTool(IDocumentReader reader, DocumentGraphDbContext dbContext)
    {
        _reader = reader;
        _dbContext = dbContext;
    }

    public string Name => "outline";

    public string Description =>
        "Inspect the structural outline of an indexed document without loading full text. " +
        "Returns the document's table of contents, sections, pages, sheets, or code symbols.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            file = new
            {
                type = "string",
                description = "Path or filename of the document to inspect."
            },
            id = new
            {
                type = "integer",
                description = "Optional document ID."
            }
        }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        string? file = null;
        if (arguments.TryGetProperty("file", out var fileProp) && !string.IsNullOrWhiteSpace(fileProp.GetString()))
        {
            file = fileProp.GetString();
        }

        if (string.IsNullOrWhiteSpace(file) && arguments.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var idVal))
        {
            var doc = await _dbContext.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == idVal, cancellationToken);
            if (doc != null)
            {
                file = doc.Path;
            }
        }

        if (string.IsNullOrWhiteSpace(file))
        {
            return McpToolCallResult.Text("Error: Either 'file' or 'id' must be specified.", isError: true);
        }

        var pathCheck = DocumentGraph.Core.Security.PathGuard.ValidatePath(file);
        if (!pathCheck.IsValid)
        {
            return McpToolCallResult.Text($"Security Error: {pathCheck.ErrorMessage}", isError: true);
        }

        var outline = await _reader.GetOutlineAsync(file, cancellationToken);
        if (outline == null)
        {
            return McpToolCallResult.Text($"Document '{file}' was not found in the index.", isError: true);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Outline: {outline.Title}");
        sb.AppendLine($"File: {outline.Filename} ({outline.Path})");
        sb.AppendLine($"Type: {outline.DocumentType}");
        sb.AppendLine("---");

        if (outline.Sections.Count > 0)
        {
            sb.AppendLine("\n## Structural Sections:");
            foreach (var s in outline.Sections)
            {
                string indent = new string(' ', Math.Max(0, s.Depth * 2));
                string loc = "";
                if (s.PageNumber.HasValue) loc = $" [Page {s.PageNumber.Value}]";
                else if (s.SlideNumber.HasValue) loc = $" [Slide {s.SlideNumber.Value}]";
                else if (!string.IsNullOrWhiteSpace(s.SheetName)) loc = $" [Sheet '{s.SheetName}']";

                sb.AppendLine($"{indent}- {s.Title}{loc}");
            }
        }

        if (outline.Symbols.Count > 0)
        {
            sb.AppendLine($"\n## Code Symbols ({outline.Symbols.Count}):");
            foreach (var sym in outline.Symbols)
            {
                string sig = string.IsNullOrWhiteSpace(sym.Signature) ? "" : $" -> {sym.Signature}";
                sb.AppendLine($"- [{sym.SymbolType}] {sym.Name} (lines {sym.LineStart}-{sym.LineEnd}){sig}");
            }
        }

        if (outline.Sections.Count == 0 && outline.Symbols.Count == 0)
        {
            sb.AppendLine("\n(No explicit sections or symbols recorded; file is indexed as sequential chunks.)");
        }

        return McpToolCallResult.Text(sb.ToString());
    }
}
