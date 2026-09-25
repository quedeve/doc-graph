using System.Text.RegularExpressions;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;

namespace DocumentGraph.Parsers.Text;

/// <summary>
/// Parser for Markdown (.md) files.
/// Splits content into sections based on headings (# through ######).
/// </summary>
public partial class MarkdownParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".md"];

    public bool CanParse(string extension)
        => extension.Equals(".md", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');

        var document = new ParsedDocument
        {
            Title = ExtractTitle(lines, filePath),
            SourcePath = Path.GetFullPath(filePath),
            Extension = ".md",
            DocumentType = "document",
            Language = "markdown"
        };

        var sections = ParseSections(lines);
        document.Sections = sections;

        return document;
    }

    private static string ExtractTitle(string[] lines, string filePath)
    {
        // Use the first H1 heading as title, or fall back to filename
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
            {
                return trimmed[2..].Trim();
            }
        }
        return Path.GetFileNameWithoutExtension(filePath);
    }

    private static List<DocumentSection> ParseSections(string[] lines)
    {
        var sections = new List<DocumentSection>();
        var currentTitle = "";
        var currentDepth = 0;
        var currentContent = new List<string>();
        var currentLineStart = 1;
        var sectionIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var headingMatch = HeadingRegex().Match(line);

            if (headingMatch.Success)
            {
                // Save the previous section if it has content
                if (currentContent.Count > 0 || sectionIndex > 0)
                {
                    FlushSection(sections, ref sectionIndex, currentTitle, currentDepth,
                        currentContent, currentLineStart, i);
                }

                currentTitle = headingMatch.Groups[2].Value.Trim();
                currentDepth = headingMatch.Groups[1].Value.Length;
                currentContent = [];
                currentLineStart = i + 1;
            }
            else
            {
                currentContent.Add(line);
            }
        }

        // Flush the last section
        FlushSection(sections, ref sectionIndex, currentTitle, currentDepth,
            currentContent, currentLineStart, lines.Length);

        // If no headings were found, create a single section with all content
        if (sections.Count == 0 && lines.Length > 0)
        {
            sections.Add(new DocumentSection
            {
                Title = "(document)",
                Content = string.Join('\n', lines),
                Depth = 0,
                SectionIndex = 0,
                LineStart = 1,
                LineEnd = lines.Length
            });
        }

        return sections;
    }

    private static void FlushSection(
        List<DocumentSection> sections,
        ref int sectionIndex,
        string title,
        int depth,
        List<string> contentLines,
        int lineStart,
        int lineEnd)
    {
        var content = string.Join('\n', contentLines).Trim();
        if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(title))
            return;

        sections.Add(new DocumentSection
        {
            Title = string.IsNullOrEmpty(title) ? "(preamble)" : title,
            Content = content,
            Depth = depth,
            SectionIndex = sectionIndex++,
            LineStart = lineStart,
            LineEnd = lineEnd
        });
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();
}
