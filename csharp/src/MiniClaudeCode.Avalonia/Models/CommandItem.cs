namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a command in the command palette.
/// </summary>
public class CommandItem
{
    /// <summary>Unique command identifier.</summary>
    public string Id { get; init; } = "";

    /// <summary>Display label.</summary>
    public string Label { get; init; } = "";

    /// <summary>Category for grouping (e.g. "File", "Edit", "Git").</summary>
    public string Category { get; init; } = "";

    /// <summary>Keyboard shortcut display string.</summary>
    public string Shortcut { get; init; } = "";

    /// <summary>The action to execute.</summary>
    public Action? Execute { get; init; }

    /// <summary>Display string combining category and label.</summary>
    public string DisplayText => string.IsNullOrEmpty(Category) ? Label : $"{Category}: {Label}";
}
