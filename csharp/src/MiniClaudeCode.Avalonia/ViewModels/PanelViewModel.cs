using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

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
