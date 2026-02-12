using System.Diagnostics;

namespace MiniClaudeCode.Avalonia.Services.Git;

/// <summary>
/// Computes git gutter decorations (added/modified/deleted lines) for the editor.
/// Compares current file content with HEAD to determine line-level changes.
/// </summary>
public class GitGutterService
{
    private string? _workDir;

    public void SetWorkDirectory(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>
    /// Get line-level git decorations for a file.
    /// Returns a dictionary of line number -> decoration type.
    /// </summary>
    public async Task<Dictionary<int, GutterDecoration>> GetGutterDecorationsAsync(string filePath)
    {
        var result = new Dictionary<int, GutterDecoration>();
        if (string.IsNullOrEmpty(_workDir)) return result;

        try
        {
            // Get diff with unified context 0 for precise line ranges
            var relativePath = Path.GetRelativePath(_workDir, filePath).Replace('\\', '/');
            var diffOutput = await RunGitAsync($"diff -U0 HEAD -- \"{relativePath}\"");

            if (string.IsNullOrEmpty(diffOutput)) return result;

            ParseDiffForGutter(diffOutput, result);
        }
        catch
        {
            // Not in a git repo or git not available
        }

        return result;
    }

    private static void ParseDiffForGutter(string diff, Dictionary<int, GutterDecoration> result)
    {
        foreach (var line in diff.Split('\n'))
        {
            if (!line.StartsWith("@@")) continue;

            // Parse hunk header: @@ -oldStart,oldCount +newStart,newCount @@
            var parts = line.Split(' ');
            if (parts.Length < 3) continue;

            var oldPart = parts[1]; // -start,count
            var newPart = parts[2]; // +start,count

            int oldCount = 0, newStart = 1, newCount = 0;

            if (oldPart.StartsWith('-'))
            {
                var nums = oldPart[1..].Split(',');
                oldCount = nums.Length > 1 && int.TryParse(nums[1], out var oc) ? oc : 1;
            }
            if (newPart.StartsWith('+'))
            {
                var nums = newPart[1..].Split(',');
                if (int.TryParse(nums[0], out var ns)) newStart = ns;
                newCount = nums.Length > 1 && int.TryParse(nums[1], out var nc) ? nc : 1;
            }

            if (oldCount == 0 && newCount > 0)
            {
                // Pure addition
                for (int i = 0; i < newCount; i++)
                    result[newStart + i] = GutterDecoration.Added;
            }
            else if (oldCount > 0 && newCount == 0)
            {
                // Pure deletion - mark at the line where deletion happened
                result[newStart] = GutterDecoration.Deleted;
            }
            else
            {
                // Modification
                for (int i = 0; i < newCount; i++)
                    result[newStart + i] = GutterDecoration.Modified;
            }
        }
    }

    private async Task<string> RunGitAsync(string arguments)
    {
        if (string.IsNullOrEmpty(_workDir)) return "";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workDir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "";

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch
        {
            return "";
        }
    }
}

/// <summary>
/// Type of git gutter decoration for a line.
/// </summary>
public enum GutterDecoration
{
    /// <summary>Green bar - line was added.</summary>
    Added,
    /// <summary>Blue bar - line was modified.</summary>
    Modified,
    /// <summary>Red triangle - line(s) were deleted here.</summary>
    Deleted
}
