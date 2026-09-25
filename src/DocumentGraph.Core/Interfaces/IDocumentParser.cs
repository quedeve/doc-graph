using DocumentGraph.Core.Models;

namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Common interface for all document/code parsers.
/// Each parser converts a specific file type into the normalized <see cref="ParsedDocument"/> model.
/// Parsers must NOT write directly to the database.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Returns true if this parser can handle the given file extension.
    /// Extension includes the dot (e.g., ".pdf", ".cs").
    /// </summary>
    bool CanParse(string extension);

    /// <summary>
    /// The file extensions this parser supports (e.g., [".pdf"], [".cs"]).
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Parse the file at the given path into a normalized document model.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A normalized <see cref="ParsedDocument"/>.</returns>
    Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
