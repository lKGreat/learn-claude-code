using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the sidebar area. Manages which panel is currently visible.
/// Panels are identified by string Id: "explorer", "search", "scm", "extensions", "chat".
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    public const double DefaultWidth = 280;
    public const double MinAllowedWidth = 220;
    public const double MaxAllowedWidth = 420;

    [ObservableProperty]
    private string? _activePanelId = "explorer";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _width = DefaultWidth;

    public double MinWidth => MinAllowedWidth;
    public double MaxWidth => MaxAllowedWidth;

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

    partial void OnWidthChanged(double value)
    {
        var clamped = Math.Clamp(value, MinAllowedWidth, MaxAllowedWidth);
        if (Math.Abs(clamped - value) > 0.01)
        {
            Width = clamped;
        }
    }

    /// <summary>
    /// Set the active panel. Pass null to collapse the sidebar.
    /// </summary>
    public void SetActivePanel(string? panelId)
    {
        ActivePanelId = panelId;
    }
}
