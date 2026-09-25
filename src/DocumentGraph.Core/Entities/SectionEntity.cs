namespace DocumentGraph.Core.Entities;

/// <summary>
/// Database entity representing a structural section within a document.
/// </summary>
public class SectionEntity
{
    public long Id { get; set; }
    public long DocumentId { get; set; }

    /// <summary>
    /// Parent section ID for hierarchical nesting. Null for top-level sections.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Zero-based order within siblings.
    /// </summary>
    public int SectionIndex { get; set; }

    /// <summary>
    /// Section heading or title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string? SheetName { get; set; }

    /// <summary>
    /// Nesting depth (1 = top-level heading).
    /// </summary>
    public int Depth { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation properties
    public DocumentEntity Document { get; set; } = null!;
    public SectionEntity? Parent { get; set; }
    public List<SectionEntity> Children { get; set; } = [];
    public List<ChunkEntity> Chunks { get; set; } = [];
}
