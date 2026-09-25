namespace DocumentGraph.Core.Entities;

/// <summary>
/// Database entity representing an indexed document.
/// </summary>
public class DocumentEntity
{
    public long Id { get; set; }

    /// <summary>
    /// Absolute file path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Filename without directory.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// File extension including dot (e.g., ".pdf").
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// MIME type (e.g., "application/pdf").
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Document title extracted from content.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// High-level classification: "code", "document", "spreadsheet", "presentation", "data".
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// SHA-256 hash of file content.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Last file modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// When this document was last indexed.
    /// </summary>
    public DateTime IndexedAt { get; set; }

    /// <summary>
    /// Name of the parser used (e.g., "PdfParser", "MarkdownParser").
    /// </summary>
    public string? Parser { get; set; }

    /// <summary>
    /// Programming language or document language.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Arbitrary JSONB metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation properties
    public List<SectionEntity> Sections { get; set; } = [];
    public List<ChunkEntity> Chunks { get; set; } = [];
    public List<SymbolEntity> Symbols { get; set; } = [];
}
