namespace DocumentGraph.Core.CodeGraph;

/// <summary>
/// Symbol information extracted or augmented by CodeGraph.
/// </summary>
public class CodeGraphSymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public string SymbolType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string? VerbatimSource { get; set; }
    public List<string> Callers { get; set; } = [];
    public List<string> Callees { get; set; } = [];
}

/// <summary>
/// Result from exploring symbols via CodeGraph.
/// </summary>
public class CodeGraphExploreResult
{
    public bool IsAvailable { get; set; }
    public string Query { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public List<CodeGraphSymbolInfo> Symbols { get; set; } = [];
}

/// <summary>
/// Bridge interface connecting DocumentGraph with CodeGraph for Unified Knowledge Graph capabilities.
/// </summary>
public interface ICodeGraphBridge
{
    /// <summary>
    /// Checks whether CodeGraph is installed and whether a .codegraph index exists.
    /// </summary>
    bool IsCodeGraphAvailable(string? workspacePath = null);

    /// <summary>
    /// Explores code symbols and dynamic call graphs using CodeGraph.
    /// </summary>
    Task<CodeGraphExploreResult> ExploreAsync(string query, string? workspacePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes the CodeGraph index for the given workspace.
    /// </summary>
    Task<bool> SyncAsync(string? workspacePath = null, CancellationToken cancellationToken = default);
}
