using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// Core coding tools: bash, read_file, write_file, edit_file, grep.
/// These 5 tools cover 95% of coding agent use cases.
/// </summary>
public class CodingPlugin
{
    private readonly string _workDir;
    private readonly IOutputSink _output;
    private const int MaxOutputBytes = 50_000;
    private const int DefaultTimeoutSeconds = 60;

    public CodingPlugin(string workDir, IOutputSink output)
    {
        _workDir = workDir;
        _output = output;
    }

    // =========================================================================
    // Path Safety
    // =========================================================================

    private string SafePath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_workDir, relativePath));
        var workDirFull = Path.GetFullPath(_workDir);

        if (!full.StartsWith(workDirFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes workspace: {relativePath}");

        return full;
    }

    private static string Truncate(string text, int maxLen = MaxOutputBytes)
    {
        return text.Length > maxLen
            ? text[..maxLen] + $"\n... (truncated, {text.Length} total chars)"
            : text;
    }

    // =========================================================================
    // Tool 1: Bash
    // =========================================================================

    [KernelFunction("bash")]
    [Description("Run a shell command. Use for: ls, find, git, npm, python, curl, etc.")]
    public async Task<string> RunBash(
        [Description("The shell command to execute")] string command)
    {
        string[] dangerous = ["rm -rf /", "sudo rm -rf", "shutdown", "reboot", "> /dev/"];
        if (dangerous.Any(d => command.Contains(d, StringComparison.OrdinalIgnoreCase)))
            return "Error: Dangerous command blocked";

        _output.WriteDebug($"$ {command}");

        try
        {
            string shell, shellArgs;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shell = "cmd.exe";
                shellArgs = $"/c {command}";
            }
            else
            {
                shell = "/bin/bash";
                shellArgs = $"-c \"{command.Replace("\"", "\\\"")}\"";
            }

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                WorkingDirectory = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit(DefaultTimeoutSeconds * 1000);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                return $"Error: Command timed out ({DefaultTimeoutSeconds}s)";
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = (stdout + stderr).Trim();

            if (!string.IsNullOrEmpty(output))
                _output.WriteDebug(output.Length > 500 ? output[..500] + "..." : output);
            else
                _output.WriteDebug("(no output)");

            return Truncate(string.IsNullOrEmpty(output) ? "(no output)" : output);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // =========================================================================
    // Tool 2: Read File
    // =========================================================================

    [KernelFunction("read_file")]
    [Description("Read file contents. Returns UTF-8 text with optional line limit for large files.")]
    public string ReadFile(
        [Description("Relative path to the file")] string path,
        [Description("Max lines to read (default: all)")] int? limit = null)
    {
        try
        {
            var fullPath = SafePath(path);
            var text = File.ReadAllText(fullPath);
            var lines = text.Split('\n');

            if (limit.HasValue && limit.Value < lines.Length)
            {
                var truncated = lines.Take(limit.Value).ToList();
                truncated.Add($"... ({lines.Length - limit.Value} more lines)");
                return Truncate(string.Join('\n', truncated));
            }

            return Truncate(text);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // =========================================================================
    // Tool 3: Write File
    // =========================================================================

    [KernelFunction("write_file")]
    [Description("Write content to a file. Creates parent directories if needed.")]
    public string WriteFile(
        [Description("Relative path for the file")] string path,
        [Description("Content to write")] string content)
    {
        try
        {
            var fullPath = SafePath(path);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            return $"Wrote {content.Length} bytes to {path}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // =========================================================================
    // Tool 4: Edit File
    // =========================================================================

    [KernelFunction("edit_file")]
    [Description("Replace exact text in a file. Use for surgical edits. Only replaces the first occurrence.")]
    public string EditFile(
        [Description("Relative path to the file")] string path,
        [Description("Exact text to find (must match precisely)")] string oldText,
        [Description("Replacement text")] string newText)
    {
        try
        {
            var fullPath = SafePath(path);
            var content = File.ReadAllText(fullPath);

            if (!content.Contains(oldText))
                return $"Error: Text not found in {path}";

            var index = content.IndexOf(oldText, StringComparison.Ordinal);
            var newContent = string.Concat(content.AsSpan(0, index), newText, content.AsSpan(index + oldText.Length));

            File.WriteAllText(fullPath, newContent);
            return $"Edited {path}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // =========================================================================
    // Tool 5: Grep
    // =========================================================================

    [KernelFunction("grep")]
    [Description("Search files for a pattern (regex). Returns matching lines in file:line:content format.")]
    public string Grep(
        [Description("Regex pattern to search for")] string pattern,
        [Description("File or directory path to search (default: current directory)")] string? path = null,
        [Description("Case insensitive search (default: false)")] bool ignoreCase = false,
        [Description("Number of context lines before and after match (default: 0)")] int contextLines = 0)
    {
        try
        {
            var searchPath = string.IsNullOrEmpty(path) ? _workDir : SafePath(path);
            var options = RegexOptions.Compiled;
            if (ignoreCase)
                options |= RegexOptions.IgnoreCase;

            Regex regex;
            try
            {
                regex = new Regex(pattern, options);
            }
            catch (RegexParseException ex)
            {
                return $"Error: Invalid regex pattern: {ex.Message}";
            }

            var results = new StringBuilder();
            int matchCount = 0;

            IEnumerable<string> files;
            if (File.Exists(searchPath))
            {
                files = [searchPath];
            }
            else if (Directory.Exists(searchPath))
            {
                files = Directory.EnumerateFiles(searchPath, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                });
            }
            else
            {
                return $"Error: Path not found: {path}";
            }

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(_workDir, file);
                if (ShouldSkipFile(relativePath))
                    continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!regex.IsMatch(lines[i]))
                        continue;

                    matchCount++;
                    int start = Math.Max(0, i - contextLines);
                    int end = Math.Min(lines.Length - 1, i + contextLines);

                    for (int j = start; j <= end; j++)
                    {
                        var separator = j == i ? ":" : "-";
                        results.AppendLine($"{relativePath}{separator}{j + 1}{separator}{lines[j]}");
                    }

                    if (contextLines > 0)
                        results.AppendLine("--");

                    if (results.Length > MaxOutputBytes)
                    {
                        results.AppendLine($"\n... (truncated, {matchCount}+ matches found)");
                        return results.ToString();
                    }
                }
            }

            if (matchCount == 0)
                return "No matches found.";

            results.AppendLine($"\n({matchCount} matches)");
            return results.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static bool ShouldSkipFile(string relativePath)
    {
        string[] skipDirs = [".git", "node_modules", "bin", "obj", ".vs", "__pycache__", ".venv", "venv"];
        foreach (var dir in skipDirs)
        {
            if (relativePath.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(dir + '/', StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("/" + dir + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        string[] binaryExts = [".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
                               ".zip", ".tar", ".gz", ".7z", ".rar",
                               ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
                               ".pdf", ".doc", ".docx", ".xls", ".xlsx",
                               ".mp3", ".mp4", ".avi", ".mov", ".wav",
                               ".woff", ".woff2", ".ttf", ".eot",
                               ".nupkg", ".snupkg"];

        var ext = Path.GetExtension(relativePath);
        return binaryExts.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }
}
