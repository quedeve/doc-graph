using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Security;
using Xunit;

namespace DocumentGraph.Tests;

public class SecurityTests
{
    [Fact]
    public void PathGuard_RejectsNullOrEmptyPath()
    {
        var result = PathGuard.ValidatePath("");
        Assert.False(result.IsValid);
        Assert.Contains("cannot be null or empty", result.ErrorMessage);
    }

    [Fact]
    public void PathGuard_RejectsRestrictedExtensions()
    {
        var result = PathGuard.ValidatePath("secrets.env");
        Assert.False(result.IsValid);
        Assert.Contains("restricted by security policy", result.ErrorMessage);

        var keyResult = PathGuard.ValidatePath("private.key");
        Assert.False(keyResult.IsValid);
        Assert.Contains("restricted by security policy", keyResult.ErrorMessage);

        var exeResult = PathGuard.ValidatePath("payload.exe");
        Assert.False(exeResult.IsValid);
        Assert.Contains("restricted by security policy", exeResult.ErrorMessage);
    }

    [Fact]
    public void PathGuard_RejectsPathOutsideAllowedRoots()
    {
        var config = new SecurityConfig
        {
            EnforceAllowedRoots = true,
            AllowedRoots = [@"C:\SafeDirectory"]
        };

        var result = PathGuard.ValidatePath(@"C:\Windows\notepad.txt", config);
        Assert.False(result.IsValid);
        Assert.Contains("outside the allowed workspace roots", result.ErrorMessage);
    }

    [Fact]
    public void PathGuard_AcceptsContainedPath()
    {
        var baseDir = Path.GetFullPath(".");
        var config = new SecurityConfig
        {
            EnforceAllowedRoots = true,
            AllowedRoots = [baseDir]
        };

        var candidate = Path.Combine(baseDir, "tests", "data", "architecture.md");
        var result = PathGuard.ValidatePath(candidate, config);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(candidate, result.NormalizedPath);
    }

    [Fact]
    public void ContentSanitizer_StripsNullBytesAndControlCharacters()
    {
        string malicious = "Hello\0World\u0001\u0002\u0003Test\u001FDone";
        string sanitized = ContentSanitizer.SanitizeText(malicious);

        Assert.Equal("HelloWorldTestDone", sanitized);
        Assert.False(sanitized.Contains('\0'));
        Assert.False(sanitized.Contains('\u0001'));
    }

    [Fact]
    public void ContentSanitizer_PreservesNormalWhitespace()
    {
        string normal = "Line 1\r\nLine 2\tIndented";
        string sanitized = ContentSanitizer.SanitizeText(normal);

        Assert.Equal(normal, sanitized);
    }
}
