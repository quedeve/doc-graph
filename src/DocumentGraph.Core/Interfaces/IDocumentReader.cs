namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Service providing structured slice reading into indexed documents:
/// entire document, pages, sections, slides, sheets, ranges, code symbols, or line windows.
/// </summary>
public interface IDocumentReader
{
    /// <summary>
    /// Read document metadata and structural outline (sections, pages, slides, sheets, symbols).
    /// </summary>
    Task<DocumentOutline?> GetOutlineAsync(string pathOrFilename, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read structured content according to specified request parameters.
    /// </summary>
    Task<ReadContentResult?> ReadAsync(ReadContentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request parameters for structured reading.
/// </summary>
public class ReadContentRequest
{
    public string PathOrFilename { get; set; } = string.Empty;
    public int? Page { get; set; }
    public int? Slide { get; set; }
    public string? Sheet { get; set; }
    public string? Section { get; set; }
    public string? Symbol { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
}

/// <summary>
/// Result of a structured read operation.
/// </summary>
public class ReadContentResult
{
    public string Path { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TargetDescription { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int EstimatedTokens { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Document outline showing structural units and symbols.
/// </summary>
public class DocumentOutline
{
    public string Path { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<OutlineItem> Sections { get; set; } = [];
    public List<OutlineSymbol> Symbols { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class OutlineItem
{
    public string Title { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string? SheetName { get; set; }
    public int Depth { get; set; }
}

public class OutlineSymbol
{
    public string Name { get; set; } = string.Empty;
    public string SymbolType { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
}
