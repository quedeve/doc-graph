namespace DocumentGraph.Core.Entities;

/// <summary>
/// Database entity representing a relationship between two indexed items.
/// Relationships are polymorphic — source and target can be documents, sections, chunks, or symbols.
/// </summary>
public class RelationshipEntity
{
    public long Id { get; set; }

    /// <summary>
    /// The ID of the source item.
    /// </summary>
    public long SourceId { get; set; }

    /// <summary>
    /// The type of the source item: "Document", "Section", "Chunk", "Symbol".
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the target item.
    /// </summary>
    public long TargetId { get; set; }

    /// <summary>
    /// The type of the target item: "Document", "Section", "Chunk", "Symbol".
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// Relationship kind: "CONTAINS", "REFERENCES", "CALLS", "IMPLEMENTS",
    /// "MENTIONS", "DESCRIBES", "SPECIFIED_BY", "RELATED_TO", "DEPENDS_ON", "DEFINED_IN".
    /// </summary>
    public string RelationshipType { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for extracted/inferred relationships (0.0 to 1.0).
    /// Deterministic relationships (CONTAINS, etc.) should be 1.0.
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    public Dictionary<string, object>? Metadata { get; set; }
}
