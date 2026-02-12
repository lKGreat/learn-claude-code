using System.Text.Json;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Manages workspace (project folder) lifecycle: recent list, open/switch, state persistence.
/// </summary>
public class WorkspaceService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".miniclaudecode");

    private static readonly string WorkspacesFile = Path.Combine(ConfigDir, "workspaces.json");

    private const int MaxRecent = 20;

    /// <summary>
    /// Current workspace path. Null if no workspace is open.
    /// </summary>
    public string? CurrentWorkspace { get; private set; }

    /// <summary>
    /// Load the recent workspaces list from disk.
    /// </summary>
    public List<WorkspaceInfo> LoadRecentWorkspaces()
    {
        try
        {
            if (!File.Exists(WorkspacesFile))
                return [];

            var json = File.ReadAllText(WorkspacesFile);
            return JsonSerializer.Deserialize<List<WorkspaceInfo>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Save a workspace as recently opened (moves to top of list).
    /// </summary>
    public void AddRecentWorkspace(string path)
    {
        var recent = LoadRecentWorkspaces();

        // Remove existing entry if present
        recent.RemoveAll(w => w.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        // Add at top
        recent.Insert(0, new WorkspaceInfo
        {
            Path = path,
            Name = Path.GetFileName(path) ?? path,
            LastOpened = DateTime.Now
        });

        // Trim to max
        if (recent.Count > MaxRecent)
            recent = recent.Take(MaxRecent).ToList();

        SaveRecentWorkspaces(recent);
    }

    /// <summary>
    /// Set the current workspace path.
    /// </summary>
    public void SetCurrentWorkspace(string path)
    {
        CurrentWorkspace = path;
        AddRecentWorkspace(path);
    }

    private void SaveRecentWorkspaces(List<WorkspaceInfo> workspaces)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(workspaces, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(WorkspacesFile, json);
        }
        catch
        {
            // Silently fail - not critical
        }
    }
}
