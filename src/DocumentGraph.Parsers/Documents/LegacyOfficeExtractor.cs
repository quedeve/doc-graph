using System.Text;

namespace DocumentGraph.Parsers.Documents;

/// <summary>
/// Helper to extract clean text strings and structured paragraphs from legacy binary Office files (.doc, .ppt, .xls).
/// </summary>
public static class LegacyOfficeExtractor
{
    /// <summary>
    /// Extracts readable text sequences from raw binary data, handling both UTF-16LE and 8-bit ASCII encodings.
    /// </summary>
    public static List<string> ExtractTextStrings(byte[] bytes, int minLength = 4)
    {
        if (bytes == null || bytes.Length == 0)
            return [];

        var results = new List<string>();
        var currentUtf16 = new StringBuilder();
        var currentAscii = new StringBuilder();

        // 1. Scan UTF-16LE runs (Word 97-2004 and PowerPoint 97-2003 character streams)
        for (int i = 0; i < bytes.Length - 1; i += 2)
        {
            byte low = bytes[i];
            byte high = bytes[i + 1];

            if (high == 0 && (low >= 32 && low <= 126 || low == '\r' || low == '\n' || low == '\t'))
            {
                currentUtf16.Append((char)low);
            }
            else
            {
                if (currentUtf16.Length >= minLength)
                {
                    var text = currentUtf16.ToString().Trim();
                    if (IsMeaningful(text))
                    {
                        results.Add(text);
                    }
                }
                currentUtf16.Clear();
            }
        }

        if (currentUtf16.Length >= minLength)
        {
            var text = currentUtf16.ToString().Trim();
            if (IsMeaningful(text))
            {
                results.Add(text);
            }
        }

        // 2. Scan 8-bit ASCII runs
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if ((b >= 32 && b <= 126) || b == '\r' || b == '\n' || b == '\t')
            {
                currentAscii.Append((char)b);
            }
            else
            {
                if (currentAscii.Length >= minLength)
                {
                    var text = currentAscii.ToString().Trim();
                    if (IsMeaningful(text))
                    {
                        results.Add(text);
                    }
                }
                currentAscii.Clear();
            }
        }

        if (currentAscii.Length >= minLength)
        {
            var text = currentAscii.ToString().Trim();
            if (IsMeaningful(text))
            {
                results.Add(text);
            }
        }

        // Filter and deduplicate
        return results
            .Where(s => !s.StartsWith("Microsoft") || !s.EndsWith("Document"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsMeaningful(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        int letterOrDigit = 0;
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c)) letterOrDigit++;
        }

        // Must have at least 3 letters/digits and reasonable ratio of printable text
        return letterOrDigit >= 3 && (double)letterOrDigit / text.Length >= 0.4;
    }
}
