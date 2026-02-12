namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a workspace (project folder) that the user can open.
/// </summary>
public class WorkspaceInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; } = DateTime.Now;

    /// <summary>
    /// Returns just the folder name for display.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name)
        ? System.IO.Path.GetFileName(Path) ?? Path
        : Name;
}
