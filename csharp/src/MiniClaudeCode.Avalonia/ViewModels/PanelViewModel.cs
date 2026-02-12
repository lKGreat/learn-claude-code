using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Represents a problem/diagnostic item (error, warning, info).
/// </summary>
public class ProblemItem
{
    public string Severity { get; init; } = "Error"; // Error, Warning, Info
    public string Message { get; init; } = "";
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public string Source { get; init; } = ""; // e.g. "roslyn", "eslint"

    public string SeverityIcon => Severity switch
    {
        "Error" => "\u2716",
        "Warning" => "\u26A0",
        "Info" => "\u2139",
        _ => "\u25CF"
    };

    public string SeverityColor => Severity switch
    {
        "Error" => "#F87171",
        "Warning" => "#FBBF24",
        "Info" => "#89B4FA",
        _ => "#CDD6F4"
    };

    public string LocationDisplay => Line > 0 ? $"[Ln {Line}, Col {Column}]" : "";
}

/// <summary>
/// Represents an output channel entry.
/// </summary>
public class OutputEntry
{
    public string Channel { get; init; } = "Main";
    public string Message { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss");
}

/// <summary>
/// ViewModel for the bottom panel area (Terminal, Problems, Output, Tool Calls, Agents, Todos).
/// Mimics VS Code's bottom panel with tabs.
/// </summary>
public partial class PanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _activeTabId = "terminal";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _height = 250;

    [ObservableProperty]
    private bool _isMaximized;

    // Problems panel
    public ObservableCollection<ProblemItem> Problems { get; } = [];
    public bool HasProblems => Problems.Count > 0;
    public int ErrorCount => Problems.Count(p => p.Severity == "Error");
    public int WarningCount => Problems.Count(p => p.Severity == "Warning");
    public string ProblemsHeader => $"Problems ({Problems.Count})";

    // Output panel
    public ObservableCollection<OutputEntry> OutputEntries { get; } = [];
    public ObservableCollection<string> OutputChannels { get; } = ["Main", "Extensions", "Git"];

    [ObservableProperty]
    private string _activeOutputChannel = "Main";

    /// <summary>Add a problem/diagnostic.</summary>
    public void AddProblem(string severity, string message, string file, int line = 0, int column = 0, string source = "")
    {
        Services.DispatcherService.Post(() =>
        {
            Problems.Add(new ProblemItem
            {
                Severity = severity,
                Message = message,
                File = file,
                Line = line,
                Column = column,
                Source = source
            });
            OnPropertyChanged(nameof(HasProblems));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(ProblemsHeader));
        });
    }

    /// <summary>Clear all problems (called from external code on dispatcher).</summary>
    public void ClearAllProblems()
    {
        Services.DispatcherService.Post(() =>
        {
            Problems.Clear();
            OnPropertyChanged(nameof(HasProblems));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(ProblemsHeader));
        });
    }

    /// <summary>Write to an output channel.</summary>
    public void WriteOutput(string message, string channel = "Main")
    {
        Services.DispatcherService.Post(() =>
        {
            OutputEntries.Add(new OutputEntry
            {
                Channel = channel,
                Message = message
            });

            if (!OutputChannels.Contains(channel))
                OutputChannels.Add(channel);
        });
    }

    [RelayCommand]
    private void ClearOutput()
    {
        OutputEntries.Clear();
    }

    [RelayCommand]
    private void ClearProblems()
    {
        Problems.Clear();
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ProblemsHeader));
    }

    // Tab visibility helpers
    public bool IsTerminalTab => ActiveTabId == "terminal";
    public bool IsProblemsTab => ActiveTabId == "problems";
    public bool IsOutputTab => ActiveTabId == "output";
    public bool IsToolCallsTab => ActiveTabId == "toolcalls";
    public bool IsAgentsTab => ActiveTabId == "agents";
    public bool IsTodosTab => ActiveTabId == "todos";

    partial void OnActiveTabIdChanged(string value)
    {
        OnPropertyChanged(nameof(IsTerminalTab));
        OnPropertyChanged(nameof(IsProblemsTab));
        OnPropertyChanged(nameof(IsOutputTab));
        OnPropertyChanged(nameof(IsToolCallsTab));
        OnPropertyChanged(nameof(IsAgentsTab));
        OnPropertyChanged(nameof(IsTodosTab));
    }

    [RelayCommand]
    public void SwitchTab(string tabId)
    {
        ActiveTabId = tabId;
        if (!IsVisible) IsVisible = true;
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void ToggleMaximize()
    {
        IsMaximized = !IsMaximized;
    }

    [RelayCommand]
    private void ClosePanel()
    {
        IsVisible = false;
    }
}
