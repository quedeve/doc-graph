namespace DocumentGraph.Core.Models;

/// <summary>
/// The normalized intermediate representation produced by every parser.
/// All file types — PDF, DOCX, XLSX, PPTX, code, CSV, etc. — are converted
/// into this common model before entering the indexing pipeline.
/// </summary>
public class ParsedDocument
{
    /// <summary>
    /// Document title extracted from the file content or derived from filename.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the source file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// File extension including the dot (e.g., ".pdf", ".cs").
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// High-level classification: "code", "document", "spreadsheet", "presentation", "data".
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the file content, used for incremental indexing.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Last modification timestamp of the source file.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Programming language or document language if detectable (e.g., "csharp", "typescript", "en").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Structured content sections extracted from the document.
    /// </summary>
    public List<DocumentSection> Sections { get; set; } = [];

    /// <summary>
    /// Code symbols extracted from source files (namespaces, classes, methods, etc.).
    /// Empty for non-code documents.
    /// </summary>
    public List<CodeSymbol> Symbols { get; set; } = [];

    /// <summary>
    /// Arbitrary key-value metadata extracted during parsing.
    /// Examples: author, page count, slide count, sheet names.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
