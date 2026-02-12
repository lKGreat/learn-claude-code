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
    public const double DefaultWidth = 280;
    public const double MinAllowedWidth = 220;
    public const double MaxAllowedWidth = 420;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private double _width = DefaultWidth;

    public double MinWidth => MinAllowedWidth;
    public double MaxWidth => MaxAllowedWidth;

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

    partial void OnWidthChanged(double value)
    {
        var clamped = Math.Clamp(value, MinAllowedWidth, MaxAllowedWidth);
        if (Math.Abs(clamped - value) > 0.01)
        {
            Width = clamped;
        }
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
