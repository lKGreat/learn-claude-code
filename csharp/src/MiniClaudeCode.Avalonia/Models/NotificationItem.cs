using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

public enum NotificationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents a notification toast / center item.
/// </summary>
public partial class NotificationItem : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Message { get; init; } = "";
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public DateTime Timestamp { get; init; } = DateTime.Now;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _progress = -1; // -1 = no progress bar

    public bool HasProgress => Progress >= 0;

    public string SeverityIcon => Severity switch
    {
        NotificationSeverity.Info => "\u2139",    // ℹ
        NotificationSeverity.Warning => "\u26A0", // ⚠
        NotificationSeverity.Error => "\u2716",   // ✖
        _ => "\u2139"
    };

    public string SeverityColor => Severity switch
    {
        NotificationSeverity.Info => "#60A5FA",
        NotificationSeverity.Warning => "#FBBF24",
        NotificationSeverity.Error => "#F87171",
        _ => "#60A5FA"
    };
}
