using System.Diagnostics;

namespace MiniClaudeCode.Avalonia.Services.Git;

/// <summary>
/// Represents a Git worktree.
/// Mirrors VS Code's Worktree interface from git.ts.
/// </summary>
public class Worktree
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Ref { get; init; } = "";
    public bool IsMain { get; init; }
    public bool IsLocked { get; init; }
    public bool IsBare { get; init; }
}

/// <summary>
/// Worktree errors matching VS Code's error handling.
/// </summary>
public enum WorktreeError
{
    None,
    WorktreeContainsChanges,
    WorktreeAlreadyExists,
    WorktreeBranchAlreadyUsed
}

/// <summary>
/// Git worktree operations service.
/// Replicates VS Code's worktree functionality from git.ts and commands.ts.
/// </summary>
public class GitWorktreeService
{
    private readonly GitService _gitService;
    private string? _workDir;

    /// <summary>Maximum number of worktrees to auto-detect. Configurable.</summary>
    public int DetectWorktreesLimit { get; set; } = 5;

    /// <summary>Whether to detect worktrees automatically.</summary>
    public bool DetectWorktrees { get; set; } = true;

    public GitWorktreeService(GitService gitService)
    {
        _gitService = gitService;
    }

    public void SetWorkDirectory(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>
    /// Detect the kind of repository: "repository", "worktree", or "submodule".
    /// Based on VS Code's Repository.kind detection in git.ts.
    /// </summary>
    public async Task<string> DetectRepositoryKindAsync()
    {
        if (string.IsNullOrEmpty(_workDir)) return "repository";

        // Check for .git file (worktree or submodule indicator)
        var gitPath = System.IO.Path.Combine(_workDir, ".git");

        if (File.Exists(gitPath))
        {
            // .git is a file, not a directory - could be worktree or submodule
            var content = await File.ReadAllTextAsync(gitPath);
            if (content.StartsWith("gitdir:"))
            {
                var gitDir = content["gitdir:".Length..].Trim();
                // If it points to worktrees/ subdirectory, it's a worktree
                if (gitDir.Contains("/worktrees/") || gitDir.Contains("\\worktrees\\"))
                    return "worktree";
                // If it points to modules/ subdirectory, it's a submodule
                if (gitDir.Contains("/modules/") || gitDir.Contains("\\modules\\"))
                    return "submodule";
            }
        }

        return "repository";
    }

    /// <summary>
    /// List all worktrees for the current repository.
    /// Based on VS Code's getWorktrees/getWorktreesFS in git.ts.
    /// </summary>
    public async Task<List<Worktree>> GetWorktreesAsync()
    {
        if (string.IsNullOrEmpty(_workDir)) return [];

        var result = await RunGitAsync("worktree list --porcelain");
        if (result.ExitCode != 0) return [];

        return ParseWorktreeList(result.Output);
    }

    /// <summary>
    /// Create a new worktree.
    /// Based on VS Code's addWorktree in git.ts and createWorktree command.
    /// </summary>
    public async Task<(bool success, WorktreeError error, string message)> CreateWorktreeAsync(
        string path, string? branch = null, bool createBranch = false)
    {
        if (string.IsNullOrEmpty(_workDir))
            return (false, WorktreeError.None, "No working directory set");

        string args;
        if (createBranch && !string.IsNullOrEmpty(branch))
        {
            args = $"worktree add -b \"{branch}\" \"{path}\"";
        }
        else if (!string.IsNullOrEmpty(branch))
        {
            args = $"worktree add \"{path}\" \"{branch}\"";
        }
        else
        {
            args = $"worktree add \"{path}\"";
        }

        var result = await RunGitAsync(args);

        if (result.ExitCode != 0)
        {
            var error = WorktreeError.None;
            if (result.Error.Contains("already exists"))
                error = WorktreeError.WorktreeAlreadyExists;
            else if (result.Error.Contains("already checked out"))
                error = WorktreeError.WorktreeBranchAlreadyUsed;

            return (false, error, result.Error);
        }

        return (true, WorktreeError.None, "");
    }

    /// <summary>
    /// Delete a worktree.
    /// Based on VS Code's deleteWorktree in git.ts.
    /// </summary>
    public async Task<(bool success, WorktreeError error, string message)> DeleteWorktreeAsync(
        string path, bool force = false)
    {
        if (string.IsNullOrEmpty(_workDir))
            return (false, WorktreeError.None, "No working directory set");

        var args = force
            ? $"worktree remove --force \"{path}\""
            : $"worktree remove \"{path}\"";

        var result = await RunGitAsync(args);

        if (result.ExitCode != 0)
        {
            var error = result.Error.Contains("contains modified or untracked files")
                ? WorktreeError.WorktreeContainsChanges
                : WorktreeError.None;
            return (false, error, result.Error);
        }

        return (true, WorktreeError.None, "");
    }

    /// <summary>
    /// Lock a worktree to prevent pruning.
    /// </summary>
    public async Task<bool> LockWorktreeAsync(string path, string? reason = null)
    {
        var args = reason != null
            ? $"worktree lock --reason \"{reason}\" \"{path}\""
            : $"worktree lock \"{path}\"";
        var result = await RunGitAsync(args);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Unlock a worktree.
    /// </summary>
    public async Task<bool> UnlockWorktreeAsync(string path)
    {
        var result = await RunGitAsync($"worktree unlock \"{path}\"");
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Prune stale worktree entries.
    /// </summary>
    public async Task<bool> PruneWorktreesAsync()
    {
        var result = await RunGitAsync("worktree prune");
        return result.ExitCode == 0;
    }

    private static List<Worktree> ParseWorktreeList(string output)
    {
        var worktrees = new List<Worktree>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? path = null;
        string? refName = null;
        bool isBare = false;
        bool isLocked = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("worktree "))
            {
                // Save previous worktree if any
                if (path != null)
                {
                    worktrees.Add(new Worktree
                    {
                        Name = System.IO.Path.GetFileName(path),
                        Path = path,
                        Ref = refName ?? "",
                        IsMain = worktrees.Count == 0,
                        IsLocked = isLocked,
                        IsBare = isBare
                    });
                }

                path = line["worktree ".Length..];
                refName = null;
                isBare = false;
                isLocked = false;
            }
            else if (line.StartsWith("HEAD "))
            {
                // HEAD commit hash - we use branch ref instead
            }
            else if (line.StartsWith("branch "))
            {
                refName = line["branch ".Length..];
                // Convert refs/heads/main -> main
                if (refName.StartsWith("refs/heads/"))
                    refName = refName["refs/heads/".Length..];
            }
            else if (line.Trim() == "bare")
            {
                isBare = true;
            }
            else if (line.Trim() == "locked")
            {
                isLocked = true;
            }
            else if (line.StartsWith("detached"))
            {
                refName = "(detached)";
            }
        }

        // Don't forget the last entry
        if (path != null)
        {
            worktrees.Add(new Worktree
            {
                Name = System.IO.Path.GetFileName(path),
                Path = path,
                Ref = refName ?? "",
                IsMain = worktrees.Count == 0,
                IsLocked = isLocked,
                IsBare = isBare
            });
        }

        return worktrees;
    }

    private async Task<GitResult> RunGitAsync(string arguments)
    {
        if (string.IsNullOrEmpty(_workDir))
            return new GitResult(1, "", "No working directory set");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new GitResult(1, "", "Failed to start git process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(10000));

            return new GitResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new GitResult(1, "", ex.Message);
        }
    }
}
