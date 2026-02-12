using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents an agent in the agent panel.
/// </summary>
public partial class AgentItem : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string AgentType { get; init; } = string.Empty;
    public DateTime StartTime { get; init; } = DateTime.Now;

    [ObservableProperty]
    private string _status = "Running";

    [ObservableProperty]
    private string _elapsed = "0:00";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private int _toolCallCount;

    [ObservableProperty]
    private bool _isExpanded;

    public string StatusIcon => Status switch
    {
        "Running" => "\u25b6",    // Play
        "Completed" => "\u2713",  // Checkmark
        "Failed" => "\u2717",     // Cross
        _ => "\u25cf"             // Bullet
    };

    public string StatusColor => Status switch
    {
        "Running" => "#60A5FA",
        "Completed" => "#34D399",
        "Failed" => "#F87171",
        _ => "#9CA3AF"
    };

    public string TypeBadgeColor => AgentType switch
    {
        "explore" => "#8B5CF6",        // Purple
        "generalPurpose" => "#3B82F6", // Blue
        "code" => "#10B981",           // Green
        "plan" => "#F59E0B",           // Yellow
        _ => "#6B7280"
    };
}
