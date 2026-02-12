using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a workspace configuration, mirroring VS Code's .code-workspace format.
/// File extension: .mcw (MiniClaudeCode Workspace)
/// </summary>
public class WorkspaceConfiguration
{
    [JsonPropertyName("folders")]
    public List<WorkspaceFolder> Folders { get; set; } = [];

    [JsonPropertyName("settings")]
    public Dictionary<string, JsonElement> Settings { get; set; } = [];

    [JsonPropertyName("extensions")]
    public WorkspaceExtensions Extensions { get; set; } = new();

    [JsonPropertyName("launch")]
    public Dictionary<string, JsonElement> Launch { get; set; } = [];

    [JsonPropertyName("tasks")]
    public Dictionary<string, JsonElement> Tasks { get; set; } = [];
}

/// <summary>
/// A folder entry in the workspace configuration.
/// Mirrors VS Code's IWorkspaceFolder.
/// </summary>
public class WorkspaceFolder
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonIgnore]
    public string DisplayName => Name ?? System.IO.Path.GetFileName(Path) ?? Path;

    [JsonIgnore]
    public string ResolvedPath { get; set; } = "";

    [JsonIgnore]
    public int Index { get; set; }
}

/// <summary>
/// Extension recommendations for a workspace.
/// </summary>
public class WorkspaceExtensions
{
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = [];

    [JsonPropertyName("unwantedRecommendations")]
    public List<string> UnwantedRecommendations { get; set; } = [];
}

/// <summary>
/// Tracks workspace trust state, matching VS Code's workspace trust model.
/// </summary>
public enum WorkspaceTrustState
{
    Unknown,
    Trusted,
    Untrusted
}

/// <summary>
/// Represents a recent workspace entry for the welcome page / quick open.
/// </summary>
public class RecentWorkspace
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsMultiRoot { get; set; }
    public DateTime LastOpened { get; set; }
}
