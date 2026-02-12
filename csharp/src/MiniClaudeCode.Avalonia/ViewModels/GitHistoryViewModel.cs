using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Services.Git;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Represents a single commit in the git history.
/// </summary>
public class GitCommitItem
{
    public string Hash { get; init; } = "";
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
    public string Message { get; init; } = "";
    public string Author { get; init; } = "";
    public string Date { get; init; } = "";
    public string Refs { get; init; } = ""; // branch/tag decorations
    public bool HasRefs => !string.IsNullOrEmpty(Refs);
}

/// <summary>
/// ViewModel for the Git History panel showing commit log.
/// </summary>
public partial class GitHistoryViewModel : ObservableObject
{
    private readonly GitService _gitService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private GitCommitItem? _selectedCommit;

    [ObservableProperty]
    private string _commitDetail = "";

    public ObservableCollection<GitCommitItem> Commits { get; } = [];
    public ObservableCollection<string> SelectedCommitFiles { get; } = [];

    /// <summary>Fired when user clicks a file in a commit to view its diff.</summary>
    public event Action<string, string>? CommitFileDiffRequested;

    public GitHistoryViewModel()
    {
        _gitService = new GitService();
    }

    public void SetWorkspace(string path)
    {
        _gitService.SetWorkDirectory(path);
    }

    [RelayCommand]
    private async Task LoadHistory()
    {
        IsLoading = true;
        try
        {
            // Get detailed log
            var result = await RunGitAsync(
                "log --pretty=format:%H%n%s%n%an%n%ad%n%D --date=relative -n 100");

            Commits.Clear();
            if (string.IsNullOrEmpty(result)) return;

            var lines = result.Split('\n');
            for (int i = 0; i + 4 <= lines.Length; i += 5)
            {
                var hash = lines[i].Trim();
                var message = lines[i + 1].Trim();
                var author = lines[i + 2].Trim();
                var date = lines[i + 3].Trim();
                var refs = i + 4 < lines.Length ? lines[i + 4].Trim() : "";

                if (string.IsNullOrEmpty(hash)) continue;

                Commits.Add(new GitCommitItem
                {
                    Hash = hash,
                    Message = message,
                    Author = author,
                    Date = date,
                    Refs = refs
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCommitChanged(GitCommitItem? value)
    {
        if (value != null)
        {
            _ = LoadCommitDetailAsync(value);
        }
    }

    private async Task LoadCommitDetailAsync(GitCommitItem commit)
    {
        SelectedCommitFiles.Clear();

        // Get files changed in this commit
        var result = await RunGitAsync($"diff-tree --no-commit-id --name-status -r {commit.Hash}");
        if (string.IsNullOrEmpty(result)) return;

        foreach (var line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2)
            {
                var status = parts[0] switch
                {
                    "M" => "M",
                    "A" => "A",
                    "D" => "D",
                    "R" => "R",
                    _ => "?"
                };
                SelectedCommitFiles.Add($"{status}  {parts[1]}");
            }
        }

        CommitDetail = $"{commit.ShortHash} - {commit.Message}\n" +
                        $"Author: {commit.Author}\n" +
                        $"Date: {commit.Date}\n" +
                        $"Files changed: {SelectedCommitFiles.Count}";
    }

    [RelayCommand]
    private void ViewCommitFileDiff(string? fileEntry)
    {
        if (fileEntry == null || SelectedCommit == null) return;

        // Parse "M  filename" format
        var parts = fileEntry.Split("  ", 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            CommitFileDiffRequested?.Invoke(SelectedCommit.Hash, parts[1].Trim());
        }
    }

    [RelayCommand]
    private void Show()
    {
        IsVisible = true;
        _ = LoadHistory();
    }

    [RelayCommand]
    private void Hide()
    {
        IsVisible = false;
    }

    private async Task<string> RunGitAsync(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = GetWorkDir(),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
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

    private string GetWorkDir()
    {
        // Use reflection to access the private _workDir field since GitService doesn't expose it
        // Fallback: use cwd
        return Directory.GetCurrentDirectory();
    }
}
