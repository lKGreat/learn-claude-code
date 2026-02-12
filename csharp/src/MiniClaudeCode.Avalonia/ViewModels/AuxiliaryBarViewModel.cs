using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Auxiliary Bar (right sidebar).
/// Provides secondary views like AI Chat, Outline, Timeline.
/// Toggled with Ctrl+Alt+B.
/// </summary>
public partial class AuxiliaryBarViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private double _width = 300;

    [ObservableProperty]
    private string _activePanel = "chat";

    public bool IsChatPanel => ActivePanel == "chat";
    public bool IsOutlinePanel => ActivePanel == "outline";
    public bool IsTimelinePanel => ActivePanel == "timeline";

    partial void OnActivePanelChanged(string value)
    {
        OnPropertyChanged(nameof(IsChatPanel));
        OnPropertyChanged(nameof(IsOutlinePanel));
        OnPropertyChanged(nameof(IsTimelinePanel));
    }

    [RelayCommand]
    private void Toggle()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void SwitchPanel(string panelId)
    {
        ActivePanel = panelId;
        if (!IsVisible) IsVisible = true;
    }
}
