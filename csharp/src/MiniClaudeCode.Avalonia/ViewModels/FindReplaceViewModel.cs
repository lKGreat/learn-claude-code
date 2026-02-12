using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Find and Replace overlay panel.
/// </summary>
public partial class FindReplaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _replaceText = "";

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isReplaceVisible;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isWholeWord;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private int _matchCount;

    [ObservableProperty]
    private int _currentMatchIndex = -1;

    public string ResultSummary
    {
        get
        {
            if (MatchCount == 0)
                return "No results";
            if (CurrentMatchIndex < 0)
                return $"{MatchCount} results";
            return $"{CurrentMatchIndex + 1} of {MatchCount}";
        }
    }

    // Events
    public event Action<string, bool, bool, bool>? SearchRequested;
    public event Action? FindNextRequested;
    public event Action? FindPreviousRequested;
    public event Action<string>? ReplaceCurrentRequested;
    public event Action? ReplaceAllRequested;
    public event Action? CloseRequested;

    [RelayCommand]
    private void FindNext() => FindNextRequested?.Invoke();

    [RelayCommand]
    private void FindPrevious() => FindPreviousRequested?.Invoke();

    [RelayCommand]
    private void ReplaceCurrent() => ReplaceCurrentRequested?.Invoke(ReplaceText);

    [RelayCommand]
    private void ReplaceAll() => ReplaceAllRequested?.Invoke();

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleReplace() => IsReplaceVisible = !IsReplaceVisible;

    [RelayCommand]
    private void ToggleCase() => IsCaseSensitive = !IsCaseSensitive;

    [RelayCommand]
    private void ToggleWholeWord() => IsWholeWord = !IsWholeWord;

    [RelayCommand]
    private void ToggleRegex() => IsRegex = !IsRegex;

    partial void OnSearchTextChanged(string value) => TriggerSearch();
    partial void OnIsCaseSensitiveChanged(bool value) => TriggerSearch();
    partial void OnIsWholeWordChanged(bool value) => TriggerSearch();
    partial void OnIsRegexChanged(bool value) => TriggerSearch();

    partial void OnMatchCountChanged(int value) => OnPropertyChanged(nameof(ResultSummary));
    partial void OnCurrentMatchIndexChanged(int value) => OnPropertyChanged(nameof(ResultSummary));

    private void TriggerSearch() =>
        SearchRequested?.Invoke(SearchText, IsCaseSensitive, IsWholeWord, IsRegex);
}
