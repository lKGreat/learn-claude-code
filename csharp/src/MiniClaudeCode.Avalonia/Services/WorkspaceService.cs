using System.Text.Json;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Service for managing multi-root workspace operations.
/// Mirrors VS Code's IWorkspaceContextService from workspace.ts.
/// </summary>
public class WorkspaceService
{
    private WorkspaceConfiguration? _currentConfig;
    private readonly List<WorkspaceFolder> _resolvedFolders = [];
    private string? _workspaceFilePath;

    /// <summary>Whether the current workspace is multi-root.</summary>
    public bool IsMultiRoot => _resolvedFolders.Count > 1;

    /// <summary>Whether a workspace file is open.</summary>
    public bool HasWorkspaceFile => _workspaceFilePath != null;

    /// <summary>The workspace file path (.mcw).</summary>
    public string? WorkspaceFilePath => _workspaceFilePath;

    /// <summary>Current workspace trust state.</summary>
    public WorkspaceTrustState TrustState { get; private set; } = WorkspaceTrustState.Unknown;

    /// <summary>Resolved workspace folders.</summary>
    public IReadOnlyList<WorkspaceFolder> Folders => _resolvedFolders.AsReadOnly();

    /// <summary>Current configuration.</summary>
    public WorkspaceConfiguration? Configuration => _currentConfig;

    /// <summary>Fired when the workspace folders change.</summary>
    public event Action? WorkspaceFoldersChanged;

    /// <summary>Fired when the trust state changes.</summary>
    public event Action<WorkspaceTrustState>? TrustStateChanged;

    /// <summary>
    /// Open a single folder as a workspace.
    /// </summary>
    public void OpenFolder(string folderPath)
    {
        _currentConfig = new WorkspaceConfiguration
        {
            Folders = [new WorkspaceFolder { Path = folderPath }]
        };
        _workspaceFilePath = null;

        ResolveFolders(folderPath);
        WorkspaceFoldersChanged?.Invoke();
    }

    /// <summary>
    /// Open a .mcw workspace file.
    /// </summary>
    public async Task OpenWorkspaceFileAsync(string mcwFilePath)
    {
        var json = await File.ReadAllTextAsync(mcwFilePath);
        _currentConfig = JsonSerializer.Deserialize<WorkspaceConfiguration>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _workspaceFilePath = mcwFilePath;
        var baseDir = Path.GetDirectoryName(mcwFilePath) ?? ".";

        ResolveFolders(baseDir);
        WorkspaceFoldersChanged?.Invoke();
    }

    /// <summary>
    /// Save current workspace configuration to a .mcw file.
    /// </summary>
    public async Task SaveWorkspaceFileAsync(string mcwFilePath)
    {
        if (_currentConfig == null) return;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(_currentConfig, options);
        await File.WriteAllTextAsync(mcwFilePath, json);
        _workspaceFilePath = mcwFilePath;
    }

    /// <summary>
    /// Add a folder to the workspace (multi-root).
    /// </summary>
    public void AddFolder(string folderPath, string? name = null)
    {
        if (_currentConfig == null)
        {
            _currentConfig = new WorkspaceConfiguration();
        }

        var folder = new WorkspaceFolder
        {
            Path = folderPath,
            Name = name
        };

        _currentConfig.Folders.Add(folder);

        // Resolve the new folder
        folder.ResolvedPath = Path.GetFullPath(folderPath);
        folder.Index = _resolvedFolders.Count;
        _resolvedFolders.Add(folder);

        WorkspaceFoldersChanged?.Invoke();
    }

    /// <summary>
    /// Remove a folder from the workspace.
    /// </summary>
    public void RemoveFolder(int index)
    {
        if (_currentConfig == null || index < 0 || index >= _currentConfig.Folders.Count)
            return;

        _currentConfig.Folders.RemoveAt(index);
        _resolvedFolders.RemoveAt(index);

        // Re-index
        for (int i = 0; i < _resolvedFolders.Count; i++)
            _resolvedFolders[i].Index = i;

        WorkspaceFoldersChanged?.Invoke();
    }

    /// <summary>
    /// Set workspace trust state.
    /// </summary>
    public void SetTrustState(WorkspaceTrustState state)
    {
        TrustState = state;
        TrustStateChanged?.Invoke(state);
    }

    /// <summary>
    /// Get a workspace setting value.
    /// </summary>
    public T? GetSetting<T>(string key)
    {
        if (_currentConfig?.Settings == null || !_currentConfig.Settings.ContainsKey(key))
            return default;

        try
        {
            return _currentConfig.Settings[key].Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Get recent workspaces from user data.
    /// </summary>
    public static List<RecentWorkspace> GetRecentWorkspaces()
    {
        var path = GetRecentWorkspacesFilePath();
        if (!File.Exists(path)) return [];

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<RecentWorkspace>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Save to recent workspaces list.
    /// </summary>
    public void SaveToRecentWorkspaces()
    {
        var name = _workspaceFilePath != null
            ? Path.GetFileNameWithoutExtension(_workspaceFilePath)
            : _resolvedFolders.FirstOrDefault()?.DisplayName ?? "Workspace";

        var workspacePath = _workspaceFilePath ?? _resolvedFolders.FirstOrDefault()?.ResolvedPath ?? "";

        var recent = GetRecentWorkspaces();
        recent.RemoveAll(r => r.Path == workspacePath);
        recent.Insert(0, new RecentWorkspace
        {
            Path = workspacePath,
            Name = name,
            IsMultiRoot = IsMultiRoot,
            LastOpened = DateTime.UtcNow
        });

        // Keep only last 20
        if (recent.Count > 20)
            recent = recent.Take(20).ToList();

        try
        {
            var json = JsonSerializer.Serialize(recent, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(GetRecentWorkspacesFilePath());
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(GetRecentWorkspacesFilePath(), json);
        }
        catch { /* ignore */ }
    }

    private void ResolveFolders(string baseDir)
    {
        _resolvedFolders.Clear();

        if (_currentConfig?.Folders == null) return;

        for (int i = 0; i < _currentConfig.Folders.Count; i++)
        {
            var folder = _currentConfig.Folders[i];
            folder.ResolvedPath = Path.IsPathRooted(folder.Path)
                ? folder.Path
                : Path.GetFullPath(Path.Combine(baseDir, folder.Path));
            folder.Index = i;
            _resolvedFolders.Add(folder);
        }
    }

    private static string GetRecentWorkspacesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MiniClaudeCode", "recent-workspaces.json");
    }
}
