using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Search result model.
/// </summary>
public class SearchResult
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string MatchPreview => $"{LineNumber}: {LineContent.Trim()}";
}

/// <summary>
/// ViewModel for the sidebar search panel (Ctrl+Shift+F).
/// </summary>
public partial class SearchPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _replaceText = "";

    [ObservableProperty]
    private string _includePattern = "";

    [ObservableProperty]
    private string _excludePattern = "";

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isWholeWord;

    [ObservableProperty]
    private bool _showReplace;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _resultsSummary = "";

    public ObservableCollection<SearchResult> Results { get; } = [];

    private string? _workspacePath;
    private CancellationTokenSource? _searchCts;

    /// <summary>Fired when user clicks on a search result to open the file.</summary>
    public event Action<string, int>? FileOpenRequested;

    public void SetWorkspace(string path)
    {
        _workspacePath = path;
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchText) || string.IsNullOrEmpty(_workspacePath))
            return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        Results.Clear();
        ResultsSummary = "Searching...";

        try
        {
            var results = await Task.Run(() => SearchFiles(_workspacePath, SearchText, ct), ct);

            Results.Clear();
            foreach (var r in results.Take(1000)) // Limit results for performance
            {
                Results.Add(r);
            }
            ResultsSummary = $"{results.Count} results in {results.Select(r => r.FilePath).Distinct().Count()} files";
        }
        catch (OperationCanceledException)
        {
            ResultsSummary = "Search cancelled";
        }
        catch (Exception ex)
        {
            ResultsSummary = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void OpenResult(SearchResult? result)
    {
        if (result != null)
            FileOpenRequested?.Invoke(result.FilePath, result.LineNumber);
    }

    [RelayCommand]
    private void ToggleReplace()
    {
        ShowReplace = !ShowReplace;
    }

    [RelayCommand]
    private void ClearResults()
    {
        Results.Clear();
        ResultsSummary = "";
        SearchText = "";
    }

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", "__pycache__",
        ".venv", "venv", ".idea", ".cursor", ".codex", ".claude"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
        ".zip", ".tar", ".gz", ".7z", ".rar",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
        ".pdf", ".mp3", ".mp4", ".avi", ".mov", ".wav",
        ".woff", ".woff2", ".ttf", ".eot", ".nupkg"
    };

    private List<SearchResult> SearchFiles(string rootPath, string searchText, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        var comparison = IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        SearchDirectory(rootPath, searchText, comparison, results, ct);
        return results;
    }

    private void SearchDirectory(string dir, string searchText, StringComparison comparison,
        List<SearchResult> results, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (results.Count > 5000) return; // Hard limit

        try
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var dirName = Path.GetFileName(subDir);
                if (SkipDirs.Contains(dirName)) continue;
                SearchDirectory(subDir, searchText, comparison, results, ct);
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                ct.ThrowIfCancellationRequested();
                var ext = Path.GetExtension(file);
                if (BinaryExtensions.Contains(ext)) continue;

                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length > 5 * 1024 * 1024) continue; // Skip files > 5MB

                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(searchText, comparison))
                        {
                            results.Add(new SearchResult
                            {
                                FilePath = file,
                                LineNumber = i + 1,
                                LineContent = lines[i]
                            });
                        }
                    }
                }
                catch
                {
                    // Skip files that can't be read
                }
            }
        }
        catch
        {
            // Skip directories that can't be accessed
        }
    }
}
