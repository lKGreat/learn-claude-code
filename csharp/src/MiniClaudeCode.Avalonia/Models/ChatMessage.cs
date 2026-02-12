using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a single message in the chat view.
/// </summary>
public enum ChatMessageRole
{
    User,
    Assistant,
    System,
    Error,
    Warning
}

public partial class ChatMessage : ObservableObject
{
    public ChatMessageRole Role { get; init; }

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string RoleLabel => Role switch
    {
        ChatMessageRole.User => "You",
        ChatMessageRole.Assistant => "Assistant",
        ChatMessageRole.System => "System",
        ChatMessageRole.Error => "Error",
        ChatMessageRole.Warning => "Warning",
        _ => "Unknown"
    };

    public string RoleColor => Role switch
    {
        ChatMessageRole.User => "#60A5FA",       // Blue
        ChatMessageRole.Assistant => "#34D399",   // Green
        ChatMessageRole.System => "#9CA3AF",      // Gray
        ChatMessageRole.Error => "#F87171",       // Red
        ChatMessageRole.Warning => "#FBBF24",     // Yellow
        _ => "#9CA3AF"
    };
}
