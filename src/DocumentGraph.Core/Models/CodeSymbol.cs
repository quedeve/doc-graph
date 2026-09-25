namespace DocumentGraph.Core.Models;

/// <summary>
/// A code symbol extracted from a source file.
/// Represents namespaces, classes, interfaces, methods, properties, functions, etc.
/// </summary>
public class CodeSymbol
{
    /// <summary>
    /// Symbol name (e.g., "MfaChallengeService", "GenerateAsync").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kind of symbol: Namespace, Class, Interface, Method, Property, Function, Constructor, Enum, Field, Type.
    /// </summary>
    public SymbolKind Kind { get; set; }

    /// <summary>
    /// Fully qualified namespace or module path.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Full signature including parameters and return type.
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Starting line number (1-based) in the source file.
    /// </summary>
    public int LineStart { get; set; }

    /// <summary>
    /// Ending line number (1-based, inclusive) in the source file.
    /// </summary>
    public int LineEnd { get; set; }

    /// <summary>
    /// The source code body of this symbol.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Child symbols (e.g., methods within a class).
    /// </summary>
    public List<CodeSymbol> Children { get; set; } = [];

    /// <summary>
    /// Arbitrary metadata for this symbol.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Classification of code symbols.
/// </summary>
public enum SymbolKind
{
    Namespace,
    Class,
    Interface,
    Struct,
    Enum,
    Method,
    Constructor,
    Property,
    Field,
    Function,
    Type,
    Variable,
    Constant,
    Event,
    Delegate
}
