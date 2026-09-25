namespace DocumentGraph.Core.Models;

/// <summary>
/// A structural unit within a parsed document.
/// Represents a heading section in DOCX, a page in PDF, a slide in PPTX,
/// a worksheet in XLSX, or a logical section in code/text files.
/// </summary>
public class DocumentSection
{
    /// <summary>
    /// Section title (heading text, slide title, sheet name, etc.).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The full text content of this section.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Nesting depth for hierarchical structures (e.g., Heading 1 = 1, Heading 2 = 2).
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Zero-based index of this section within its parent document.
    /// </summary>
    public int SectionIndex { get; set; }

    /// <summary>
    /// Page number (1-based) for PDF documents.
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// Slide number (1-based) for PPTX presentations.
    /// </summary>
    public int? SlideNumber { get; set; }

    /// <summary>
    /// Worksheet name for XLSX spreadsheets.
    /// </summary>
    public string? SheetName { get; set; }

    /// <summary>
    /// Starting line number (1-based) for text/code files.
    /// </summary>
    public int? LineStart { get; set; }

    /// <summary>
    /// Ending line number (1-based, inclusive) for text/code files.
    /// </summary>
    public int? LineEnd { get; set; }

    /// <summary>
    /// Child sections for hierarchical documents.
    /// </summary>
    public List<DocumentSection> Children { get; set; } = [];

    /// <summary>
    /// Arbitrary metadata specific to this section.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
