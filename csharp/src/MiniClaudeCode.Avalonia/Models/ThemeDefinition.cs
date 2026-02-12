namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a color theme definition with its color tokens.
/// </summary>
public sealed class ThemeDefinition
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsDark { get; init; }
    public required Dictionary<string, string> Colors { get; init; }
}
