using System.Diagnostics;
using System.Text.RegularExpressions;
using DocumentGraph.Core.CodeGraph;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Search.Services;

/// <summary>
/// Bridge implementation that communicates with CodeGraph via CLI and filesystem index.
/// Facilitates unified knowledge graph integration between documents and codebases.
/// </summary>
public class CodeGraphBridge : ICodeGraphBridge
{
    private readonly ILogger<CodeGraphBridge> _logger;

    public CodeGraphBridge(ILogger<CodeGraphBridge> logger)
    {
        _logger = logger;
    }

    public bool IsCodeGraphAvailable(string? workspacePath = null)
    {
        var root = ResolveWorkspaceRoot(workspacePath);
        if (root == null) return false;

        var codegraphDir = Path.Combine(root, ".codegraph");
        return Directory.Exists(codegraphDir);
    }

    public async Task<CodeGraphExploreResult> ExploreAsync(
        string query,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CodeGraphExploreResult
        {
            Query = query,
            IsAvailable = IsCodeGraphAvailable(workspacePath)
        };

        if (!result.IsAvailable)
        {
            _logger.LogDebug("CodeGraph index (.codegraph) not found for query '{Query}'.", query);
            return result;
        }

        var root = ResolveWorkspaceRoot(workspacePath) ?? Directory.GetCurrentDirectory();

        try
        {
            string fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "codegraph";
            string arguments = OperatingSystem.IsWindows()
                ? $"/c codegraph explore \"{EscapeCommandLineArg(query)}\""
                : $"explore \"{EscapeCommandLineArg(query)}\"";

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                result.Output = stdout.Trim();
                ParseSymbolsFromOutput(result);
            }
            else if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning("CodeGraph explore exited with code {Code}: {Stderr}", process.ExitCode, stderr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run 'codegraph explore' for '{Query}'", query);
        }

        return result;
    }

    public async Task<bool> SyncAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var root = ResolveWorkspaceRoot(workspacePath) ?? Directory.GetCurrentDirectory();

        try
        {
            string fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "codegraph";
            string arguments = OperatingSystem.IsWindows() ? "/c codegraph sync" : "sync";

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute 'codegraph sync'");
            return false;
        }
    }

    private static string? ResolveWorkspaceRoot(string? initialPath)
    {
        var start = initialPath ?? Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(start, ".codegraph")))
        {
            return Path.GetFullPath(start);
        }

        var dir = new DirectoryInfo(Path.GetFullPath(start));
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        while (dir != null && !string.Equals(dir.FullName, userHome, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".codegraph")))
            {
                return dir.FullName;
            }

            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                break;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string EscapeCommandLineArg(string arg)
    {
        return arg.Replace("\"", "\\\"");
    }

    private static void ParseSymbolsFromOutput(CodeGraphExploreResult result)
    {
        // Parse symbol definitions or file paths from CodeGraph explore output
        // CodeGraph outputs formatted file headers like:
        // ## filename.cs (lines X-Y) or class/method signatures
        var lines = result.Output.Split('\n');
        string currentFile = string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("## ") || trimmed.StartsWith("=== "))
            {
                currentFile = trimmed.TrimStart('#', '=', ' ');
            }
            else if (trimmed.Contains("class ") || trimmed.Contains("interface ") || trimmed.Contains("record "))
            {
                var match = Regex.Match(trimmed, @"(class|interface|record|struct)\s+([A-Za-z0-9_]+)");
                if (match.Success)
                {
                    result.Symbols.Add(new CodeGraphSymbolInfo
                    {
                        SymbolType = match.Groups[1].Value,
                        Name = match.Groups[2].Value,
                        FilePath = currentFile
                    });
                }
            }
        }
    }
}
