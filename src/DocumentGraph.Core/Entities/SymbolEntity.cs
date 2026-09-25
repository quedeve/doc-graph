namespace DocumentGraph.Core.Entities;

/// <summary>
/// Database entity representing a code symbol (class, method, property, etc.).
/// </summary>
public class SymbolEntity
{
    public long Id { get; set; }
    public long DocumentId { get; set; }

    /// <summary>
    /// Symbol name (e.g., "MfaChallengeService").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kind of symbol: "Namespace", "Class", "Interface", "Method", "Property", etc.
    /// </summary>
    public string SymbolType { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified namespace or module path.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Parent symbol ID for nesting (e.g., method inside a class).
    /// </summary>
    public long? ParentSymbolId { get; set; }

    public int LineStart { get; set; }
    public int LineEnd { get; set; }

    /// <summary>
    /// Full signature including parameters and return type.
    /// </summary>
    public string? Signature { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation properties
    public DocumentEntity Document { get; set; } = null!;
    public SymbolEntity? ParentSymbol { get; set; }
    public List<SymbolEntity> Children { get; set; } = [];
}
