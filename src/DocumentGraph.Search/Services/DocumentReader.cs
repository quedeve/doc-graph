using System.Text;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Storage.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Search.Services;

/// <summary>
/// Service implementing structured document inspection and reading.
/// </summary>
public class DocumentReader : IDocumentReader
{
    private readonly DocumentGraphDbContext _db;
    private readonly ILogger<DocumentReader> _logger;

    public DocumentReader(DocumentGraphDbContext db, ILogger<DocumentReader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DocumentOutline?> GetOutlineAsync(string pathOrFilename, CancellationToken cancellationToken = default)
    {
        var doc = await FindDocumentAsync(pathOrFilename, cancellationToken);
        if (doc == null) return null;

        var sections = await _db.Sections
            .AsNoTracking()
            .Where(s => s.DocumentId == doc.Id)
            .OrderBy(s => s.SectionIndex)
            .ToListAsync(cancellationToken);

        var symbols = await _db.Symbols
            .AsNoTracking()
            .Where(s => s.DocumentId == doc.Id)
            .OrderBy(s => s.LineStart)
            .ToListAsync(cancellationToken);

        var outline = new DocumentOutline
        {
            Path = doc.Path,
            Filename = doc.Filename,
            DocumentType = doc.DocumentType,
            Title = doc.Title ?? doc.Filename,
            Metadata = doc.Metadata ?? []
        };

        foreach (var s in sections)
        {
            outline.Sections.Add(new OutlineItem
            {
                Title = s.Title,
                PageNumber = s.PageNumber,
                SlideNumber = s.SlideNumber,
                SheetName = s.SheetName,
                Depth = s.Depth
            });
        }

        foreach (var sym in symbols)
        {
            outline.Symbols.Add(new OutlineSymbol
            {
                Name = sym.Name,
                SymbolType = sym.SymbolType,
                Signature = sym.Signature,
                LineStart = sym.LineStart,
                LineEnd = sym.LineEnd
            });
        }

        return outline;
    }

    public async Task<ReadContentResult?> ReadAsync(ReadContentRequest request, CancellationToken cancellationToken = default)
    {
        var doc = await FindDocumentAsync(request.PathOrFilename, cancellationToken);
        if (doc == null) return null;

        var query = _db.Chunks
            .AsNoTracking()
            .Where(c => c.DocumentId == doc.Id);

        string targetDesc;

        // 1. By Code Symbol
        if (!string.IsNullOrWhiteSpace(request.Symbol))
        {
            targetDesc = $"symbol '{request.Symbol}'";
            var sym = await _db.Symbols
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.DocumentId == doc.Id && EF.Functions.ILike(s.Name, $"%{request.Symbol}%"), cancellationToken);

            if (sym != null)
            {
                targetDesc = $"{sym.SymbolType} '{sym.Name}' (lines {sym.LineStart}-{sym.LineEnd})";
                // Chunks intersecting the symbol's line range or matching section title
                query = query.Where(c => 
                    (c.LineStart <= sym.LineEnd && c.LineEnd >= sym.LineStart) ||
                    (c.SectionTitle != null && EF.Functions.ILike(c.SectionTitle, $"%{sym.Name}%")));
            }
            else
            {
                query = query.Where(c => c.SectionTitle != null && EF.Functions.ILike(c.SectionTitle, $"%{request.Symbol}%"));
            }
        }
        // 2. By Page
        else if (request.Page.HasValue)
        {
            targetDesc = $"page {request.Page.Value}";
            query = query.Where(c => c.PageNumber == request.Page.Value);
        }
        // 3. By Slide
        else if (request.Slide.HasValue)
        {
            targetDesc = $"slide {request.Slide.Value}";
            query = query.Where(c => c.SlideNumber == request.Slide.Value);
        }
        // 4. By Excel Sheet
        else if (!string.IsNullOrWhiteSpace(request.Sheet))
        {
            targetDesc = $"sheet '{request.Sheet}'";
            query = query.Where(c => c.SheetName != null && EF.Functions.ILike(c.SheetName, request.Sheet));
        }
        // 5. By Section title
        else if (!string.IsNullOrWhiteSpace(request.Section))
        {
            targetDesc = $"section '{request.Section}'";
            query = query.Where(c => c.SectionTitle != null && EF.Functions.ILike(c.SectionTitle, $"%{request.Section}%"));
        }
        // 6. By Line range
        else if (request.LineStart.HasValue)
        {
            var lEnd = request.LineEnd ?? request.LineStart.Value + 50;
            targetDesc = $"lines {request.LineStart.Value}-{lEnd}";
            query = query.Where(c => c.LineStart <= lEnd && c.LineEnd >= request.LineStart.Value);
        }
        // 7. Whole document
        else
        {
            targetDesc = "full document";
        }

        var chunks = await query
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0 && (request.LineStart.HasValue || !string.IsNullOrWhiteSpace(request.Symbol)))
        {
            // Fallback: If code chunks were not split down to exact lines, read directly from on-disk source file if accessible
            if (File.Exists(doc.Path))
            {
                var fileResult = ReadDirectlyFromFile(doc, request, targetDesc);
                if (fileResult != null) return fileResult;
            }
        }

        var sb = new StringBuilder();
        int totalTokens = 0;
        foreach (var c in chunks)
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine("---").AppendLine();
            sb.Append(c.Content);
            totalTokens += c.TokenCount;
        }

        return new ReadContentResult
        {
            Path = doc.Path,
            Filename = doc.Filename,
            DocumentType = doc.DocumentType,
            Title = doc.Title ?? doc.Filename,
            TargetDescription = targetDesc,
            Content = sb.ToString(),
            TotalChunks = chunks.Count,
            EstimatedTokens = totalTokens > 0 ? totalTokens : (int)Math.Ceiling(sb.Length / 4.0),
            Metadata = doc.Metadata ?? []
        };
    }

    private async Task<DocumentEntity?> FindDocumentAsync(string pathOrFilename, CancellationToken cancellationToken)
    {
        var clean = pathOrFilename.Trim();
        // Exact path match
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Path == clean, cancellationToken);
        if (doc != null) return doc;

        // Filename match
        var filename = Path.GetFileName(clean);
        doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Filename == filename, cancellationToken);
        if (doc != null) return doc;

        // Substring path match
        return await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Path, $"%{clean}%"), cancellationToken);
    }

    private static ReadContentResult? ReadDirectlyFromFile(DocumentEntity doc, ReadContentRequest request, string targetDesc)
    {
        var validation = DocumentGraph.Core.Security.PathGuard.ValidatePath(doc.Path);
        if (!validation.IsValid) return null;

        try
        {
            var lines = File.ReadAllLines(doc.Path);
            int start = Math.Max(1, request.LineStart ?? 1);
            int end = Math.Min(lines.Length, request.LineEnd ?? lines.Length);

            var selected = lines[(start - 1)..end];
            var content = string.Join("\n", selected);

            return new ReadContentResult
            {
                Path = doc.Path,
                Filename = doc.Filename,
                DocumentType = doc.DocumentType,
                Title = doc.Title ?? doc.Filename,
                TargetDescription = targetDesc,
                Content = content,
                TotalChunks = 1,
                EstimatedTokens = (int)Math.Ceiling(content.Length / 4.0),
                Metadata = doc.Metadata ?? []
            };
        }
        catch
        {
            return null;
        }
    }
}
