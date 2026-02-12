using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a tool call entry in the tool call panel.
/// </summary>
public partial class ToolCallItem : ObservableObject
{
    public string ToolName { get; init; } = string.Empty;
    public string ArgumentsPreview { get; init; } = string.Empty;
    public string FullArguments { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    [ObservableProperty]
    private string _status = "Running";

    [ObservableProperty]
    private string _duration = "...";

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    public string StatusIcon => Status switch
    {
        "Running" => "\u25b6",   // Play symbol
        "Success" => "\u2713",   // Checkmark
        "Failed" => "\u2717",    // Cross
        _ => "\u25cf"            // Bullet
    };

    public string StatusColor => Status switch
    {
        "Running" => "#60A5FA",
        "Success" => "#34D399",
        "Failed" => "#F87171",
        _ => "#9CA3AF"
    };
}
