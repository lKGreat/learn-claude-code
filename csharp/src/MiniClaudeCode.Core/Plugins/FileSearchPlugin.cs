using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.SemanticKernel;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// File search tools: Glob pattern matching and structured directory listing.
/// </summary>
public class FileSearchPlugin
{
    private readonly string _workDir;
    private const int MaxOutputBytes = 50_000;

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", "__pycache__",
        ".venv", "venv", ".next", "dist", "build", ".idea",
        ".cursor", "coverage", ".nyc_output"
    };

    public FileSearchPlugin(string workDir)
    {
        _workDir = workDir;
    }

    private string SafePath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_workDir, relativePath));
        var workDirFull = Path.GetFullPath(_workDir);
        if (!full.StartsWith(workDirFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes workspace: {relativePath}");
        return full;
    }

    [KernelFunction("glob")]
    [Description(@"Search for files matching a glob pattern. Returns matching file paths sorted by modification time.
Patterns not starting with '**/' are automatically prepended with '**/' for recursive search.")]
    public string Glob(
        [Description("Glob pattern to match files")] string pattern,
        [Description("Directory to search in (default: workspace root)")] string? directory = null)
    {
        try
        {
            var searchDir = string.IsNullOrEmpty(directory) ? _workDir : SafePath(directory);
            if (!Directory.Exists(searchDir))
                return $"Error: Directory not found: {directory}";

            var effectivePattern = pattern;
            if (!pattern.StartsWith("**/") && !pattern.Contains('/') && !pattern.Contains('\\'))
                effectivePattern = "**/" + pattern;

            var matcher = new Matcher();
            matcher.AddInclude(effectivePattern);
            foreach (var dir in SkipDirs)
                matcher.AddExclude($"{dir}/**");

            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(searchDir)));

            if (!result.HasMatches)
                return "No files matched the pattern.";

            var files = result.Files
                .Select(f => new { f.Path, ModTime = File.GetLastWriteTimeUtc(Path.Combine(searchDir, f.Path)) })
                .OrderByDescending(f => f.ModTime)
                .ToList();

            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.AppendLine(file.Path);
                if (sb.Length > MaxOutputBytes)
                {
                    sb.AppendLine($"\n... (truncated, {files.Count} total matches)");
                    break;
                }
            }
            sb.AppendLine($"\n({files.Count} files matched)");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("list_directory")]
    [Description(@"List files and directories at a given path with a tree-like structure.")]
    public string ListDirectory(
        [Description("Path to directory to list (default: workspace root)")] string? path = null,
        [Description("Max depth to recurse (default: 1)")] int maxDepth = 1)
    {
        try
        {
            var targetDir = string.IsNullOrEmpty(path) ? _workDir : SafePath(path);
            if (!Directory.Exists(targetDir))
                return $"Error: Directory not found: {path}";

            var sb = new StringBuilder();
            sb.AppendLine($"{Path.GetFileName(targetDir)}/");
            ListDirectoryRecursive(targetDir, sb, "", maxDepth, 0);

            if (sb.Length > MaxOutputBytes)
                return sb.ToString()[..MaxOutputBytes] + "\n... (truncated)";

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private void ListDirectoryRecursive(string dir, StringBuilder sb, string indent, int maxDepth, int currentDepth)
    {
        if (currentDepth >= maxDepth) return;

        try
        {
            var entries = Directory.GetFileSystemEntries(dir)
                .Select(e => new { Path = e, Name = Path.GetFileName(e), IsDir = Directory.Exists(e) })
                .Where(e => !e.Name.StartsWith('.'))
                .Where(e => !e.IsDir || !SkipDirs.Contains(e.Name))
                .OrderBy(e => !e.IsDir)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var isLast = i == entries.Count - 1;
                var connector = isLast ? "└── " : "├── ";
                var childIndent = indent + (isLast ? "    " : "│   ");

                if (entry.IsDir)
                {
                    sb.AppendLine($"{indent}{connector}{entry.Name}/");
                    ListDirectoryRecursive(entry.Path, sb, childIndent, maxDepth, currentDepth + 1);
                }
                else
                {
                    sb.AppendLine($"{indent}{connector}{entry.Name}");
                }

                if (sb.Length > MaxOutputBytes) return;
            }
        }
        catch (UnauthorizedAccessException)
        {
            sb.AppendLine($"{indent}(access denied)");
        }
    }
}
