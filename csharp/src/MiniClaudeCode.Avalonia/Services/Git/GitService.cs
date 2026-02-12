using System.Diagnostics;

namespace MiniClaudeCode.Avalonia.Services.Git;

/// <summary>
/// Core Git operations wrapper using git CLI.
/// Replaces VS Code's git.ts extension functionality.
/// </summary>
public class GitService
{
    private string? _workDir;

    public void SetWorkDirectory(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>Detect if the directory is a git repository.</summary>
    public async Task<bool> IsGitRepoAsync()
    {
        if (string.IsNullOrEmpty(_workDir)) return false;
        var result = await RunGitAsync("rev-parse --is-inside-work-tree");
        return result.ExitCode == 0 && result.Output.Trim() == "true";
    }

    /// <summary>Get the current branch name.</summary>
    public async Task<string> GetBranchNameAsync()
    {
        var result = await RunGitAsync("rev-parse --abbrev-ref HEAD");
        return result.ExitCode == 0 ? result.Output.Trim() : "";
    }

    /// <summary>Get ahead/behind counts for current branch.</summary>
    public async Task<(int ahead, int behind)> GetAheadBehindAsync()
    {
        var result = await RunGitAsync("rev-list --count --left-right @{u}...HEAD");
        if (result.ExitCode != 0) return (0, 0);
        var parts = result.Output.Trim().Split('\t');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int behind) &&
            int.TryParse(parts[1], out int ahead))
        {
            return (ahead, behind);
        }
        return (0, 0);
    }

    /// <summary>Get porcelain status output.</summary>
    public async Task<string> GetStatusAsync()
    {
        var result = await RunGitAsync("status --porcelain");
        return result.ExitCode == 0 ? result.Output : "";
    }

    /// <summary>Stage a file.</summary>
    public Task StageAsync(string relativePath) => RunGitAsync($"add \"{relativePath}\"");

    /// <summary>Unstage a file.</summary>
    public Task UnstageAsync(string relativePath) => RunGitAsync($"reset HEAD \"{relativePath}\"");

    /// <summary>Discard changes in a file.</summary>
    public Task DiscardAsync(string relativePath) => RunGitAsync($"checkout -- \"{relativePath}\"");

    /// <summary>Stage all files.</summary>
    public Task StageAllAsync() => RunGitAsync("add -A");

    /// <summary>Unstage all files.</summary>
    public Task UnstageAllAsync() => RunGitAsync("reset HEAD");

    /// <summary>Commit with a message.</summary>
    public async Task<bool> CommitAsync(string message)
    {
        var result = await RunGitAsync($"commit -m \"{message.Replace("\"", "\\\"")}\"");
        return result.ExitCode == 0;
    }

    /// <summary>Fetch from remote.</summary>
    public Task FetchAsync() => RunGitAsync("fetch");

    /// <summary>Pull from remote.</summary>
    public Task PullAsync() => RunGitAsync("pull");

    /// <summary>Push to remote.</summary>
    public Task PushAsync() => RunGitAsync("push");

    /// <summary>Get diff of a file.</summary>
    public async Task<string> GetDiffAsync(string? relativePath = null)
    {
        var args = relativePath != null ? $"diff \"{relativePath}\"" : "diff";
        var result = await RunGitAsync(args);
        return result.ExitCode == 0 ? result.Output : "";
    }

    /// <summary>Get log entries.</summary>
    public async Task<string> GetLogAsync(int maxCount = 50)
    {
        var result = await RunGitAsync($"log --oneline --graph -n {maxCount}");
        return result.ExitCode == 0 ? result.Output : "";
    }

    /// <summary>List branches.</summary>
    public async Task<List<string>> GetBranchesAsync()
    {
        var result = await RunGitAsync("branch --list");
        if (result.ExitCode != 0) return [];
        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim().TrimStart('*').Trim())
            .ToList();
    }

    /// <summary>Checkout a branch.</summary>
    public Task CheckoutAsync(string branchName) => RunGitAsync($"checkout \"{branchName}\"");

    /// <summary>Create a new branch.</summary>
    public Task CreateBranchAsync(string branchName) => RunGitAsync($"checkout -b \"{branchName}\"");

    /// <summary>Get blame for a file.</summary>
    public async Task<string> BlameAsync(string relativePath)
    {
        var result = await RunGitAsync($"blame --porcelain \"{relativePath}\"");
        return result.ExitCode == 0 ? result.Output : "";
    }

    /// <summary>Stash current changes.</summary>
    public Task StashAsync(string? message = null)
    {
        var args = message != null ? $"stash push -m \"{message}\"" : "stash push";
        return RunGitAsync(args);
    }

    /// <summary>Pop stash.</summary>
    public Task StashPopAsync() => RunGitAsync("stash pop");

    /// <summary>List stashes.</summary>
    public async Task<string> StashListAsync()
    {
        var result = await RunGitAsync("stash list");
        return result.ExitCode == 0 ? result.Output : "";
    }

    private async Task<GitResult> RunGitAsync(string arguments, int timeoutMs = 30000)
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
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));

            return new GitResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new GitResult(1, "", ex.Message);
        }
    }
}

public record GitResult(int ExitCode, string Output, string Error);
