using DocumentGraph.Core.Configuration;

namespace DocumentGraph.Core.Security;

public class PathValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string NormalizedPath { get; set; } = string.Empty;

    public static PathValidationResult Success(string normalizedPath) =>
        new() { IsValid = true, NormalizedPath = normalizedPath };

    public static PathValidationResult Fail(string error) =>
        new() { IsValid = false, ErrorMessage = error };
}

public static class PathGuard
{
    public static PathValidationResult ValidatePath(
        string candidatePath,
        SecurityConfig? securityConfig = null,
        string? workingDirectory = null)
    {
        securityConfig ??= new SecurityConfig();

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return PathValidationResult.Fail("Path cannot be null or empty.");
        }

        // Check for invalid characters
        if (candidatePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return PathValidationResult.Fail("Path contains invalid characters.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidatePath);
        }
        catch (Exception ex)
        {
            return PathValidationResult.Fail($"Failed to resolve path: {ex.Message}");
        }

        // Check restricted extensions
        var ext = Path.GetExtension(fullPath);
        if (!string.IsNullOrEmpty(ext) &&
            securityConfig.RestrictedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return PathValidationResult.Fail($"Access to file extension '{ext}' is restricted by security policy.");
        }

        // Check allowed roots containment
        if (securityConfig.EnforceAllowedRoots)
        {
            var allowedRoots = securityConfig.AllowedRoots.Count > 0
                ? securityConfig.AllowedRoots
                : [workingDirectory ?? Directory.GetCurrentDirectory()];

            bool isContained = false;
            foreach (var root in allowedRoots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;

                var normalizedRoot = Path.GetFullPath(root);
                if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar) &&
                    !normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    normalizedRoot += Path.DirectorySeparatorChar;
                }

                if (fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fullPath, normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    isContained = true;
                    break;
                }
            }

            if (!isContained)
            {
                return PathValidationResult.Fail($"Path '{fullPath}' is outside the allowed workspace roots.");
            }
        }

        return PathValidationResult.Success(fullPath);
    }
}
