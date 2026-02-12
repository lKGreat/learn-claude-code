using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private const int CollapseThreshold = 500;
    private const int PreviewLength = 120;

    public ChatMessageRole Role { get; init; }

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _streamingElapsed = "";

    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// Whether this message can be collapsed (assistant messages longer than threshold).
    /// </summary>
    public bool IsCollapsible => Role == ChatMessageRole.Assistant && Content.Length > CollapseThreshold;

    /// <summary>
    /// Short preview text shown when collapsed.
    /// </summary>
    public string Preview => Content.Length > PreviewLength
        ? Content[..PreviewLength].TrimEnd() + "..."
        : Content;

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

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Notify that collapsible/preview may have changed after content update.
    /// </summary>
    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(IsCollapsible));
        OnPropertyChanged(nameof(Preview));
    }
}
