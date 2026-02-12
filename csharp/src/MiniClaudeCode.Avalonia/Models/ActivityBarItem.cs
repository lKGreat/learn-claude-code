using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents an item in the VS Code-style Activity Bar (left icon strip).
/// </summary>
public partial class ActivityBarItem : ObservableObject
{
    /// <summary>Unique identifier matching the sidebar panel key.</summary>
    public string Id { get; init; } = "";

    /// <summary>Display label (tooltip).</summary>
    public string Label { get; init; } = "";

    /// <summary>Icon character or path glyph.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Keyboard shortcut hint shown in tooltip.</summary>
    public string Shortcut { get; init; } = "";

    /// <summary>Sort order in the activity bar.</summary>
    public int Order { get; init; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private int _badgeCount;

    /// <summary>Whether the badge should be shown.</summary>
    public bool HasBadge => BadgeCount > 0;

    partial void OnBadgeCountChanged(int value) => OnPropertyChanged(nameof(HasBadge));
}
