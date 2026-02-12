using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Singleton settings service with user + workspace scopes.
/// </summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private readonly Dictionary<string, JsonNode?> _userSettings = new();
    private readonly Dictionary<string, JsonNode?> _workspaceSettings = new();
    private string? _workspacePath;
    private System.Threading.Timer? _saveTimer;

    public event Action<string, object?>? SettingChanged;

    // Typed convenience getters
    public string ColorTheme => Get("workbench.colorTheme", "catppuccin-mocha");
    public int FontSize => Get("editor.fontSize", 13);
    public string FontFamily => Get("editor.fontFamily", "Cascadia Code, Consolas, monospace");
    public bool MinimapEnabled => Get("editor.minimap.enabled", true);
    public int TabSize => Get("editor.tabSize", 4);
    public bool IndentGuidesEnabled => Get("editor.guides.indentation", true);
    public bool FoldingEnabled => Get("editor.folding", true);

    private SettingsService() { }

    public T Get<T>(string key, T defaultValue)
    {
        // Workspace overrides user
        if (_workspaceSettings.TryGetValue(key, out var wsVal) && wsVal != null)
        {
            try { return wsVal.Deserialize<T>() ?? defaultValue; }
            catch { /* fall through */ }
        }

        if (_userSettings.TryGetValue(key, out var userVal) && userVal != null)
        {
            try { return userVal.Deserialize<T>() ?? defaultValue; }
            catch { /* fall through */ }
        }

        return defaultValue;
    }

    public void Set(string key, object value)
    {
        _userSettings[key] = JsonSerializer.SerializeToNode(value);
        SettingChanged?.Invoke(key, value);
        DebounceSave();
    }

    public void SetWorkspaceSetting(string key, object value)
    {
        _workspaceSettings[key] = JsonSerializer.SerializeToNode(value);
        SettingChanged?.Invoke(key, value);
    }

    public void SetWorkspacePath(string? path) => _workspacePath = path;

    public void Load()
    {
        // User settings
        var userPath = GetUserSettingsPath();
        if (File.Exists(userPath))
        {
            try
            {
                var json = File.ReadAllText(userPath);
                var obj = JsonNode.Parse(json)?.AsObject();
                if (obj != null)
                {
                    foreach (var (k, v) in obj)
                        _userSettings[k] = v?.DeepClone();
                }
            }
            catch { /* ignore */ }
        }

        // Workspace settings
        if (!string.IsNullOrEmpty(_workspacePath))
        {
            var wsPath = Path.Combine(_workspacePath, ".miniclaudecode", "settings.json");
            if (File.Exists(wsPath))
            {
                try
                {
                    var json = File.ReadAllText(wsPath);
                    var obj = JsonNode.Parse(json)?.AsObject();
                    if (obj != null)
                    {
                        foreach (var (k, v) in obj)
                            _workspaceSettings[k] = v?.DeepClone();
                    }
                }
                catch { /* ignore */ }
            }
        }
    }

    public void Save()
    {
        try
        {
            var userPath = GetUserSettingsPath();
            var dir = Path.GetDirectoryName(userPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var obj = new JsonObject();
            foreach (var (k, v) in _userSettings)
                obj[k] = v?.DeepClone();

            var json = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userPath, json);
        }
        catch { /* ignore */ }
    }

    private void DebounceSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = new System.Threading.Timer(_ => Save(), null, 1000, Timeout.Infinite);
    }

    private static string GetUserSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniClaudeCode", "settings.json");
}
