using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Represents a file in the SCM view.
/// </summary>
public class ScmFileItem
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; init; } = "";
    public ScmFileStatus Status { get; init; }

    public string StatusIcon => Status switch
    {
        ScmFileStatus.Modified => "M",
        ScmFileStatus.Added => "A",
        ScmFileStatus.Deleted => "D",
        ScmFileStatus.Renamed => "R",
        ScmFileStatus.Untracked => "?",
        ScmFileStatus.Conflicted => "!",
        _ => " "
    };

    public string StatusColor => Status switch
    {
        ScmFileStatus.Modified => "#FBBF24",
        ScmFileStatus.Added => "#A6E3A1",
        ScmFileStatus.Deleted => "#F87171",
        ScmFileStatus.Renamed => "#60A5FA",
        ScmFileStatus.Untracked => "#6C7086",
        ScmFileStatus.Conflicted => "#F87171",
        _ => "#CDD6F4"
    };
}

public enum ScmFileStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted
}

/// <summary>
/// ViewModel for the Source Control Management panel.
/// </summary>
public partial class ScmViewModel : ObservableObject
{
    [ObservableProperty]
    private string _branchName = "";

    [ObservableProperty]
    private string _syncStatus = "";

    [ObservableProperty]
    private string _commitMessage = "";

    [ObservableProperty]
    private bool _isRefreshing;

    public ObservableCollection<ScmFileItem> StagedChanges { get; } = [];
    public ObservableCollection<ScmFileItem> UnstagedChanges { get; } = [];

    public string StagedHeader => $"Staged Changes ({StagedChanges.Count})";
    public string ChangesHeader => $"Changes ({UnstagedChanges.Count})";

    public int TotalChanges => StagedChanges.Count + UnstagedChanges.Count;

    private string? _workspacePath;

    /// <summary>Fired when user clicks a file to see diff.</summary>
    public event Action<string>? DiffRequested;

    public void SetWorkspace(string path)
    {
        _workspacePath = path;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;

        IsRefreshing = true;
        try
        {
            await Task.Run(() => LoadGitStatus(_workspacePath));
        }
        catch
        {
            // Git not available or not a git repo
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(StagedHeader));
            OnPropertyChanged(nameof(ChangesHeader));
            OnPropertyChanged(nameof(TotalChanges));
        }
    }

    private void LoadGitStatus(string workspacePath)
    {
        try
        {
            // Try to get branch name
            var headFile = Path.Combine(workspacePath, ".git", "HEAD");
            if (File.Exists(headFile))
            {
                var head = File.ReadAllText(headFile).Trim();
                if (head.StartsWith("ref: refs/heads/"))
                    BranchName = head["ref: refs/heads/".Length..];
                else
                    BranchName = head[..7]; // detached HEAD, show short hash
            }

            // Use git status --porcelain for file statuses
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain",
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var staged = new List<ScmFileItem>();
            var unstaged = new List<ScmFileItem>();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length < 3) continue;
                var indexStatus = line[0];
                var workStatus = line[1];
                var filePath = line[3..].Trim();

                var fullPath = Path.Combine(workspacePath, filePath.Replace('/', Path.DirectorySeparatorChar));

                // Staged changes (index status)
                if (indexStatus != ' ' && indexStatus != '?')
                {
                    staged.Add(new ScmFileItem
                    {
                        FilePath = fullPath,
                        RelativePath = filePath,
                        Status = ParseStatus(indexStatus)
                    });
                }

                // Unstaged/working tree changes
                if (workStatus != ' ' || indexStatus == '?')
                {
                    unstaged.Add(new ScmFileItem
                    {
                        FilePath = fullPath,
                        RelativePath = filePath,
                        Status = indexStatus == '?' ? ScmFileStatus.Untracked : ParseStatus(workStatus)
                    });
                }
            }

            Services.DispatcherService.Post(() =>
            {
                StagedChanges.Clear();
                foreach (var item in staged) StagedChanges.Add(item);

                UnstagedChanges.Clear();
                foreach (var item in unstaged) UnstagedChanges.Add(item);

                OnPropertyChanged(nameof(StagedHeader));
                OnPropertyChanged(nameof(ChangesHeader));
                OnPropertyChanged(nameof(TotalChanges));
            });
        }
        catch
        {
            // Not a git repo or git not installed
        }
    }

    [RelayCommand]
    private async Task Commit()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage) || string.IsNullOrEmpty(_workspacePath))
            return;

        try
        {
            await Task.Run(() =>
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"commit -m \"{CommitMessage.Replace("\"", "\\\"")}\"",
                    WorkingDirectory = _workspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(30000);
            });

            CommitMessage = "";
            await RefreshAsync();
        }
        catch
        {
            // Commit failed
        }
    }

    [RelayCommand]
    private async Task StageFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;

        await RunGitCommand($"add \"{item.RelativePath}\"");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;

        await RunGitCommand($"reset HEAD \"{item.RelativePath}\"");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DiscardFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;

        await RunGitCommand($"checkout -- \"{item.RelativePath}\"");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StageAll()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        await RunGitCommand("add -A");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageAll()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        await RunGitCommand("reset HEAD");
        await RefreshAsync();
    }

    private async Task RunGitCommand(string arguments)
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;

        await Task.Run(() =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(10000);
        });
    }

    private static ScmFileStatus ParseStatus(char c) => c switch
    {
        'M' => ScmFileStatus.Modified,
        'A' => ScmFileStatus.Added,
        'D' => ScmFileStatus.Deleted,
        'R' => ScmFileStatus.Renamed,
        'U' => ScmFileStatus.Conflicted,
        _ => ScmFileStatus.Modified
    };
}
