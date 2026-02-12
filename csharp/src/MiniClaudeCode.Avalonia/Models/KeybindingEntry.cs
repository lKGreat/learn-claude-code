namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a keyboard shortcut binding for a command.
/// </summary>
public record KeybindingEntry
{
    public required string CommandId { get; init; }
    public required string DefaultGesture { get; init; }
    public required string CurrentGesture { get; init; }
    public required string Label { get; init; }
    public required string Category { get; init; }
    public bool IsCustomized => CurrentGesture != DefaultGesture;
}
