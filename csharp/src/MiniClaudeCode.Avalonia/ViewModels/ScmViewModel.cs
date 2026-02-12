using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Services.Git;

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
/// Integrates with GitService and GitWorktreeService.
/// </summary>
public partial class ScmViewModel : ObservableObject
{
    private readonly GitService _gitService;
    private readonly GitWorktreeService _worktreeService;

    [ObservableProperty]
    private string _branchName = "";

    [ObservableProperty]
    private string _syncStatus = "";

    [ObservableProperty]
    private string _commitMessage = "";

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isGitRepo;

    [ObservableProperty]
    private string _repositoryKind = "repository";

    [ObservableProperty]
    private int _aheadCount;

    [ObservableProperty]
    private int _behindCount;

    public ObservableCollection<ScmFileItem> StagedChanges { get; } = [];
    public ObservableCollection<ScmFileItem> UnstagedChanges { get; } = [];
    public ObservableCollection<Worktree> Worktrees { get; } = [];

    public string StagedHeader => $"Staged Changes ({StagedChanges.Count})";
    public string ChangesHeader => $"Changes ({UnstagedChanges.Count})";
    public string WorktreesHeader => $"Worktrees ({Worktrees.Count})";

    public int TotalChanges => StagedChanges.Count + UnstagedChanges.Count;
    public bool HasWorktrees => Worktrees.Count > 0;

    private string? _workspacePath;

    /// <summary>Fired when user clicks a file to see diff.</summary>
    public event Action<string>? DiffRequested;

    public ScmViewModel()
    {
        _gitService = new GitService();
        _worktreeService = new GitWorktreeService(_gitService);
    }

    public void SetWorkspace(string path)
    {
        _workspacePath = path;
        _gitService.SetWorkDirectory(path);
        _worktreeService.SetWorkDirectory(path);
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
            // Check if this is a git repo
            IsGitRepo = await _gitService.IsGitRepoAsync();
            if (!IsGitRepo) return;

            // Detect repository kind (normal, worktree, submodule)
            RepositoryKind = await _worktreeService.DetectRepositoryKindAsync();

            // Get branch name and sync info
            BranchName = await _gitService.GetBranchNameAsync();
            var (ahead, behind) = await _gitService.GetAheadBehindAsync();
            AheadCount = ahead;
            BehindCount = behind;
            SyncStatus = FormatSyncStatus(ahead, behind);

            // Load file statuses
            await Task.Run(() => LoadGitStatus(_workspacePath));

            // Load worktrees
            var worktrees = await _worktreeService.GetWorktreesAsync();
            Services.DispatcherService.Post(() =>
            {
                Worktrees.Clear();
                foreach (var wt in worktrees) Worktrees.Add(wt);
                OnPropertyChanged(nameof(WorktreesHeader));
                OnPropertyChanged(nameof(HasWorktrees));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SCM refresh failed: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(StagedHeader));
            OnPropertyChanged(nameof(ChangesHeader));
            OnPropertyChanged(nameof(TotalChanges));
        }
    }

    private static string FormatSyncStatus(int ahead, int behind)
    {
        if (ahead == 0 && behind == 0) return "";
        if (ahead > 0 && behind > 0) return $"\u2191{ahead} \u2193{behind}";
        if (ahead > 0) return $"\u2191{ahead}";
        return $"\u2193{behind}";
    }

    private void LoadGitStatus(string workspacePath)
    {
        try
        {
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Git status failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task Commit()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage) || string.IsNullOrEmpty(_workspacePath))
            return;

        var success = await _gitService.CommitAsync(CommitMessage);
        if (success)
        {
            CommitMessage = "";
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task StageFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;
        await _gitService.StageAsync(item.RelativePath);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;
        await _gitService.UnstageAsync(item.RelativePath);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DiscardFile(ScmFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_workspacePath)) return;
        await _gitService.DiscardAsync(item.RelativePath);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StageAll()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        await _gitService.StageAllAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageAll()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        await _gitService.UnstageAllAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Fetch()
    {
        await _gitService.FetchAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Pull()
    {
        await _gitService.PullAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Push()
    {
        await _gitService.PushAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Stash()
    {
        await _gitService.StashAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StashPop()
    {
        await _gitService.StashPopAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private void ViewDiff(ScmFileItem? item)
    {
        if (item == null) return;
        DiffRequested?.Invoke(item.RelativePath);
    }

    /// <summary>Get list of branches (for branch picker).</summary>
    public async Task<List<string>> GetBranchListAsync()
    {
        return await _gitService.GetBranchesAsync();
    }

    /// <summary>Checkout a branch.</summary>
    public async Task<bool> CheckoutBranchAsync(string branchName)
    {
        try
        {
            await _gitService.CheckoutAsync(branchName);
            await RefreshAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Get diff for a specific file.</summary>
    public async Task<string> GetFileDiffAsync(string relativePath)
    {
        return await _gitService.GetDiffAsync(relativePath);
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
