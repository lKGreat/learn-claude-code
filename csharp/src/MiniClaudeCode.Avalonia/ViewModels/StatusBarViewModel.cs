using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the VS Code-style status bar.
/// Left: branch, problems, workspace. Right: cursor, encoding, eol, language, model, plan.
/// </summary>
public partial class StatusBarViewModel : ObservableObject
{
    // Left side
    [ObservableProperty]
    private string _branchName = "";

    [ObservableProperty]
    private string _syncStatus = ""; // e.g. "↑2 ↓3"

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private string _workspaceName = "";

    // Right side
    [ObservableProperty]
    private int _cursorLine = 1;

    [ObservableProperty]
    private int _cursorColumn = 1;

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private string _lineEnding = "LF";

    [ObservableProperty]
    private string _languageMode = "Plain Text";

    [ObservableProperty]
    private string _providerDisplay = "";

    [ObservableProperty]
    private bool _isPlanMode;

    [ObservableProperty]
    private int _turnCount;

    [ObservableProperty]
    private int _toolCallCount;

    [ObservableProperty]
    private int _notificationCount;

    // Computed display strings
    public string CursorPosition => $"Ln {CursorLine}, Col {CursorColumn}";
    public string ProblemsDisplay => $"{ErrorCount} \u2716  {WarningCount} \u26A0";
    public bool HasBranch => !string.IsNullOrEmpty(BranchName);
    public bool HasNotifications => NotificationCount > 0;

    partial void OnCursorLineChanged(int value) => OnPropertyChanged(nameof(CursorPosition));
    partial void OnCursorColumnChanged(int value) => OnPropertyChanged(nameof(CursorPosition));
    partial void OnErrorCountChanged(int value) => OnPropertyChanged(nameof(ProblemsDisplay));
    partial void OnWarningCountChanged(int value) => OnPropertyChanged(nameof(ProblemsDisplay));
    partial void OnBranchNameChanged(string value) => OnPropertyChanged(nameof(HasBranch));
    partial void OnNotificationCountChanged(int value) => OnPropertyChanged(nameof(HasNotifications));

    /// <summary>Fired when user clicks branch name to show branch picker.</summary>
    public event Action? BranchClicked;

    /// <summary>Fired when user clicks problems to show problems panel.</summary>
    public event Action? ProblemsClicked;

    /// <summary>Fired when user clicks notifications bell.</summary>
    public event Action? NotificationsClicked;

    [RelayCommand]
    private void ClickBranch() => BranchClicked?.Invoke();

    [RelayCommand]
    private void ClickProblems() => ProblemsClicked?.Invoke();

    [RelayCommand]
    private void ClickNotifications() => NotificationsClicked?.Invoke();
}
