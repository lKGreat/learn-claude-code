using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a todo item in the todo panel.
/// </summary>
public partial class TodoItem : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;

    [ObservableProperty]
    private string _status = "pending";

    public string StatusIcon => Status switch
    {
        "pending" => "\u25cb",       // Empty circle
        "in_progress" => "\u25b6",   // Play
        "completed" => "\u2713",     // Checkmark
        "cancelled" => "\u2717",     // Cross
        _ => "\u25cb"
    };

    public string StatusColor => Status switch
    {
        "pending" => "#9CA3AF",
        "in_progress" => "#60A5FA",
        "completed" => "#34D399",
        "cancelled" => "#6B7280",
        _ => "#9CA3AF"
    };
}
