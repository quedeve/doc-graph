using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Storage.PostgreSQL;

namespace DocumentGraph.Mcp.Tools;

public class OpenTool : IMcpTool
{
    private readonly IDocumentReader _reader;
    private readonly DocumentGraphDbContext _dbContext;

    public OpenTool(IDocumentReader reader, DocumentGraphDbContext dbContext)
    {
        _reader = reader;
        _dbContext = dbContext;
    }

    public string Name => "open";

    public string Description =>
        "Read structured content from an indexed document or chunk. " +
        "You can open a specific chunk by 'id' (returned from 'search'), or open a document by 'file' / document 'id' with slice options (page, slide, sheet, section, symbol, line range).";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            id = new
            {
                type = "integer",
                description = "Optional ID of a chunk or document to open directly."
            },
            file = new
            {
                type = "string",
                description = "Path or filename of the document to read."
            },
            page = new
            {
                type = "integer",
                description = "Optional page number for PDF and page-oriented documents (1-indexed)."
            },
            slide = new
            {
                type = "integer",
                description = "Optional slide number for presentations (1-indexed)."
            },
            sheet = new
            {
                type = "string",
                description = "Optional worksheet name for spreadsheets."
            },
            section = new
            {
                type = "string",
                description = "Optional section heading to read."
            },
            symbol = new
            {
                type = "string",
                description = "Optional code symbol name (method, class, interface, function) to read."
            },
            line_start = new
            {
                type = "integer",
                description = "Optional start line number (1-indexed)."
            },
            line_end = new
            {
                type = "integer",
                description = "Optional end line number (1-indexed)."
            },
            max_chars = new
            {
                type = "integer",
                description = "Maximum number of characters to return (default: 12000) to keep context compact."
            }
        }
    };

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        int maxChars = 12000;
        if (arguments.TryGetProperty("max_chars", out var mc) && mc.TryGetInt32(out var mcVal) && mcVal > 0)
        {
            maxChars = mcVal;
        }

        long? id = null;
        if (arguments.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var idVal))
        {
            id = idVal;
        }

        string? file = null;
        if (arguments.TryGetProperty("file", out var fileProp) && !string.IsNullOrWhiteSpace(fileProp.GetString()))
        {
            file = fileProp.GetString();
        }

        // 1. If id is provided, check if it's a chunk ID first
        if (id.HasValue && string.IsNullOrEmpty(file))
        {
            var chunk = await _dbContext.Chunks
                .Include(c => c.Document)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id.Value, cancellationToken);

            if (chunk != null)
            {
                string header = $"# Document: {chunk.Document?.Filename ?? "Unknown"}\n" +
                                $"Path: {chunk.Document?.Path}\n" +
                                $"Chunk ID: {chunk.Id} (Index: {chunk.ChunkIndex})\n" +
                                (chunk.PageNumber.HasValue ? $"Page: {chunk.PageNumber.Value}\n" : "") +
                                (chunk.LineStart.HasValue ? $"Lines: {chunk.LineStart}-{chunk.LineEnd}\n" : "") +
                                (!string.IsNullOrWhiteSpace(chunk.SectionTitle) ? $"Section: {chunk.SectionTitle}\n" : "") +
                                "---\n\n";

                string content = chunk.Content;
                if (content.Length > maxChars)
                {
                    content = content[..maxChars] + $"\n\n... [Truncated: {content.Length - maxChars} characters remaining. Use line range or section filter to read specific parts]";
                }

                return McpToolCallResult.Text(header + content);
            }

            // Check if it's a document ID
            var doc = await _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id.Value, cancellationToken);

            if (doc != null)
            {
                file = doc.Path;
            }
            else
            {
                return McpToolCallResult.Text($"Item with ID {id.Value} not found as chunk or document.", isError: true);
            }
        }

        if (string.IsNullOrWhiteSpace(file))
        {
            return McpToolCallResult.Text("Error: Either 'id' or 'file' must be specified.", isError: true);
        }

        var pathCheck = DocumentGraph.Core.Security.PathGuard.ValidatePath(file);
        if (!pathCheck.IsValid)
        {
            return McpToolCallResult.Text($"Security Error: {pathCheck.ErrorMessage}", isError: true);
        }

        var req = new ReadContentRequest
        {
            PathOrFilename = file
        };

        if (arguments.TryGetProperty("page", out var p) && p.TryGetInt32(out var pVal)) req.Page = pVal;
        if (arguments.TryGetProperty("slide", out var sl) && sl.TryGetInt32(out var slVal)) req.Slide = slVal;
        if (arguments.TryGetProperty("sheet", out var sh) && !string.IsNullOrWhiteSpace(sh.GetString())) req.Sheet = sh.GetString();
        if (arguments.TryGetProperty("section", out var sec) && !string.IsNullOrWhiteSpace(sec.GetString())) req.Section = sec.GetString();
        if (arguments.TryGetProperty("symbol", out var sym) && !string.IsNullOrWhiteSpace(sym.GetString())) req.Symbol = sym.GetString();
        if (arguments.TryGetProperty("line_start", out var ls) && ls.TryGetInt32(out var lsVal)) req.LineStart = lsVal;
        if (arguments.TryGetProperty("line_end", out var le) && le.TryGetInt32(out var leVal)) req.LineEnd = leVal;

        var result = await _reader.ReadAsync(req, cancellationToken);
        if (result == null)
        {
            return McpToolCallResult.Text($"Document '{file}' was not found in the index.", isError: true);
        }

        string docHeader = $"# {result.Title}\n" +
                           $"File: {result.Filename} ({result.Path})\n" +
                           $"Type: {result.DocumentType} | Target: {result.TargetDescription}\n" +
                           $"Total matching chunks: {result.TotalChunks} | Est. tokens: {result.EstimatedTokens}\n" +
                           "---\n\n";

        string docContent = result.Content;
        if (docContent.Length > maxChars)
        {
            docContent = docContent[..maxChars] + $"\n\n... [Truncated: {docContent.Length - maxChars} characters remaining. Use page, sheet, symbol, or line range to view specific parts]";
        }

        return McpToolCallResult.Text(docHeader + docContent);
    }
}
