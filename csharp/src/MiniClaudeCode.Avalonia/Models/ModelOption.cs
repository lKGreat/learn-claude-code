using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a selectable model option in the model dropdown.
/// Groups models by provider for display.
/// </summary>
public record ModelOption(ModelProvider Provider, string ModelId, string DisplayName)
{
    /// <summary>
    /// Provider group header for display (e.g., "OpenAI", "DeepSeek").
    /// </summary>
    public string ProviderName => Provider.ToString();

    /// <summary>
    /// Short label for the dropdown (e.g., "gpt-4o").
    /// </summary>
    public string ShortLabel => ModelId;

    public override string ToString() => DisplayName;
}
