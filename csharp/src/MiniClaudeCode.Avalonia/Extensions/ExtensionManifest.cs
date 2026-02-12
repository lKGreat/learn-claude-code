using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniClaudeCode.Avalonia.Extensions;

/// <summary>
/// Extension manifest (extension.json), mirroring VS Code's package.json extension format.
/// </summary>
public class ExtensionManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("main")]
    public string Main { get; set; } = "";

    [JsonPropertyName("engines")]
    public EngineRequirement Engines { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("activationEvents")]
    public List<string> ActivationEvents { get; set; } = [];

    [JsonPropertyName("contributes")]
    public ContributionPoints Contributes { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; } = [];

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }
}

public class EngineRequirement
{
    [JsonPropertyName("miniclaudecode")]
    public string MiniClaudeCode { get; set; } = ">=1.0.0";
}

/// <summary>
/// Contribution points: what the extension adds to the IDE.
/// Mirrors VS Code's contributes field in package.json.
/// </summary>
public class ContributionPoints
{
    [JsonPropertyName("commands")]
    public List<CommandContribution> Commands { get; set; } = [];

    [JsonPropertyName("menus")]
    public Dictionary<string, List<MenuContribution>> Menus { get; set; } = [];

    [JsonPropertyName("views")]
    public Dictionary<string, List<ViewContribution>> Views { get; set; } = [];

    [JsonPropertyName("languages")]
    public List<LanguageContribution> Languages { get; set; } = [];

    [JsonPropertyName("themes")]
    public List<ThemeContribution> Themes { get; set; } = [];

    [JsonPropertyName("keybindings")]
    public List<KeybindingContribution> Keybindings { get; set; } = [];

    [JsonPropertyName("configuration")]
    public ConfigurationContribution? Configuration { get; set; }

    [JsonPropertyName("snippets")]
    public List<SnippetContribution> Snippets { get; set; } = [];
}

public class CommandContribution
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public class MenuContribution
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("when")]
    public string? When { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }
}

public class ViewContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

public class LanguageContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = [];

    [JsonPropertyName("configuration")]
    public string? Configuration { get; set; }
}

public class ThemeContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("uiTheme")]
    public string UiTheme { get; set; } = "vs-dark";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

public class KeybindingContribution
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

public class ConfigurationContribution
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("properties")]
    public Dictionary<string, JsonElement> Properties { get; set; } = [];
}

public class SnippetContribution
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}
