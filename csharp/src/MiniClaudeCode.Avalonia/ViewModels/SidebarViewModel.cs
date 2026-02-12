using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the sidebar area. Manages which panel is currently visible.
/// Panels are identified by string Id: "explorer", "search", "scm", "extensions", "chat".
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _activePanelId = "explorer";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _width = 250;

    /// <summary>Show/hide individual panels based on ActivePanelId.</summary>
    public bool IsExplorerVisible => ActivePanelId == "explorer";
    public bool IsSearchVisible => ActivePanelId == "search";
    public bool IsScmVisible => ActivePanelId == "scm";
    public bool IsExtensionsVisible => ActivePanelId == "extensions";
    public bool IsChatVisible => ActivePanelId == "chat";

    partial void OnActivePanelIdChanged(string? value)
    {
        IsVisible = value != null;
        OnPropertyChanged(nameof(IsExplorerVisible));
        OnPropertyChanged(nameof(IsSearchVisible));
        OnPropertyChanged(nameof(IsScmVisible));
        OnPropertyChanged(nameof(IsExtensionsVisible));
        OnPropertyChanged(nameof(IsChatVisible));
    }

    /// <summary>
    /// Set the active panel. Pass null to collapse the sidebar.
    /// </summary>
    public void SetActivePanel(string? panelId)
    {
        ActivePanelId = panelId;
    }
}
