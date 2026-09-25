using System.Text;

namespace DocumentGraph.Core.Security;

public static class ContentSanitizer
{
    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);

        foreach (char c in input)
        {
            // Disallow null bytes
            if (c == '\0')
                continue;

            // Allow standard whitespace: tab, newline, carriage return
            if (c is '\t' or '\n' or '\r')
            {
                sb.Append(c);
                continue;
            }

            // Strip non-printable ASCII control characters (0x01 to 0x1F, except above, and 0x7F)
            if (char.IsControl(c))
                continue;

            sb.Append(c);
        }

        return sb.ToString();
    }
}
