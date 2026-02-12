using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Settings UI. Mirrors VS Code's settings editor.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _activeCategory = "Editor";

    public ObservableCollection<SettingCategory> Categories { get; } = [];
    public ObservableCollection<SettingEntry> FilteredSettings { get; } = [];

    private readonly List<SettingEntry> _allSettings = [];
    private readonly string _settingsFilePath;

    public SettingsViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsFilePath = Path.Combine(appData, "MiniClaudeCode", "settings.json");

        InitializeDefaultSettings();
        LoadSettings();
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterSettings(value);
    }

    partial void OnActiveCategoryChanged(string value)
    {
        FilterSettings(SearchQuery);
    }

    [RelayCommand]
    private void Show()
    {
        IsVisible = true;
        FilterSettings(SearchQuery);
    }

    [RelayCommand]
    private void Hide()
    {
        IsVisible = false;
        SaveSettings();
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        ActiveCategory = category;
    }

    [RelayCommand]
    private void ResetSetting(SettingEntry? entry)
    {
        if (entry == null) return;
        entry.Value = entry.DefaultValue;
        SaveSettings();
    }

    private void InitializeDefaultSettings()
    {
        // Categories
        Categories.Add(new SettingCategory { Name = "Editor", Icon = "\u270F" });
        Categories.Add(new SettingCategory { Name = "Workbench", Icon = "\u2692" });
        Categories.Add(new SettingCategory { Name = "Terminal", Icon = "\u25B6" });
        Categories.Add(new SettingCategory { Name = "Git", Icon = "\U0001F500" });
        Categories.Add(new SettingCategory { Name = "Extensions", Icon = "\U0001F9E9" });

        // Editor settings
        _allSettings.AddRange([
            new SettingEntry { Key = "editor.fontSize", Category = "Editor", Label = "Font Size", Description = "Controls the font size in pixels.", Type = SettingType.Number, DefaultValue = "13", Value = "13" },
            new SettingEntry { Key = "editor.fontFamily", Category = "Editor", Label = "Font Family", Description = "Controls the font family.", Type = SettingType.String, DefaultValue = "Cascadia Code, Consolas, monospace", Value = "Cascadia Code, Consolas, monospace" },
            new SettingEntry { Key = "editor.tabSize", Category = "Editor", Label = "Tab Size", Description = "The number of spaces a tab is equal to.", Type = SettingType.Number, DefaultValue = "4", Value = "4" },
            new SettingEntry { Key = "editor.insertSpaces", Category = "Editor", Label = "Insert Spaces", Description = "Insert spaces when pressing Tab.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "editor.wordWrap", Category = "Editor", Label = "Word Wrap", Description = "Controls if lines should wrap.", Type = SettingType.Enum, DefaultValue = "off", Value = "off", EnumValues = ["off", "on", "wordWrapColumn", "bounded"] },
            new SettingEntry { Key = "editor.lineNumbers", Category = "Editor", Label = "Line Numbers", Description = "Controls the display of line numbers.", Type = SettingType.Enum, DefaultValue = "on", Value = "on", EnumValues = ["on", "off", "relative", "interval"] },
            new SettingEntry { Key = "editor.renderWhitespace", Category = "Editor", Label = "Render Whitespace", Description = "Controls how whitespace is rendered.", Type = SettingType.Enum, DefaultValue = "selection", Value = "selection", EnumValues = ["none", "boundary", "selection", "trailing", "all"] },
            new SettingEntry { Key = "editor.minimap.enabled", Category = "Editor", Label = "Minimap Enabled", Description = "Controls whether the minimap is shown.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "editor.bracketPairColorization", Category = "Editor", Label = "Bracket Pair Colorization", Description = "Controls bracket pair colorization.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "editor.autoSave", Category = "Editor", Label = "Auto Save", Description = "Controls auto save of editors.", Type = SettingType.Enum, DefaultValue = "off", Value = "off", EnumValues = ["off", "afterDelay", "onFocusChange", "onWindowChange"] },
        ]);

        // Workbench settings
        _allSettings.AddRange([
            new SettingEntry { Key = "workbench.colorTheme", Category = "Workbench", Label = "Color Theme", Description = "Specifies the color theme used.", Type = SettingType.String, DefaultValue = "Catppuccin Mocha", Value = "Catppuccin Mocha" },
            new SettingEntry { Key = "workbench.sideBar.location", Category = "Workbench", Label = "Sidebar Location", Description = "Controls the location of the sidebar.", Type = SettingType.Enum, DefaultValue = "left", Value = "left", EnumValues = ["left", "right"] },
            new SettingEntry { Key = "workbench.activityBar.visible", Category = "Workbench", Label = "Activity Bar Visible", Description = "Controls activity bar visibility.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "workbench.statusBar.visible", Category = "Workbench", Label = "Status Bar Visible", Description = "Controls status bar visibility.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
        ]);

        // Terminal settings
        _allSettings.AddRange([
            new SettingEntry { Key = "terminal.fontFamily", Category = "Terminal", Label = "Font Family", Description = "Terminal font family.", Type = SettingType.String, DefaultValue = "Cascadia Code, Consolas, monospace", Value = "Cascadia Code, Consolas, monospace" },
            new SettingEntry { Key = "terminal.fontSize", Category = "Terminal", Label = "Font Size", Description = "Terminal font size.", Type = SettingType.Number, DefaultValue = "12", Value = "12" },
            new SettingEntry { Key = "terminal.shell", Category = "Terminal", Label = "Default Shell", Description = "The default terminal shell.", Type = SettingType.String, DefaultValue = "", Value = "" },
        ]);

        // Git settings
        _allSettings.AddRange([
            new SettingEntry { Key = "git.autofetch", Category = "Git", Label = "Auto Fetch", Description = "Periodically fetch from remotes.", Type = SettingType.Boolean, DefaultValue = "false", Value = "false" },
            new SettingEntry { Key = "git.confirmSync", Category = "Git", Label = "Confirm Sync", Description = "Confirm before syncing branches.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "git.enableSmartCommit", Category = "Git", Label = "Smart Commit", Description = "Commit all changes when no staged changes.", Type = SettingType.Boolean, DefaultValue = "false", Value = "false" },
            new SettingEntry { Key = "git.detectWorktrees", Category = "Git", Label = "Detect Worktrees", Description = "Enable automatic worktree detection.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
        ]);

        // Extensions settings
        _allSettings.AddRange([
            new SettingEntry { Key = "extensions.autoUpdate", Category = "Extensions", Label = "Auto Update", Description = "Auto update extensions.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
            new SettingEntry { Key = "extensions.autoCheckUpdates", Category = "Extensions", Label = "Auto Check Updates", Description = "Check for extension updates.", Type = SettingType.Boolean, DefaultValue = "true", Value = "true" },
        ]);
    }

    private void FilterSettings(string query)
    {
        FilteredSettings.Clear();

        var matches = _allSettings
            .Where(s => (string.IsNullOrEmpty(ActiveCategory) || s.Category == ActiveCategory) &&
                        (string.IsNullOrEmpty(query) ||
                         s.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         s.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));

        foreach (var setting in matches)
            FilteredSettings.Add(setting);
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFilePath)) return;

            var json = File.ReadAllText(_settingsFilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return;

            foreach (var entry in _allSettings)
            {
                if (dict.TryGetValue(entry.Key, out var value))
                    entry.Value = value;
            }
        }
        catch { /* use defaults */ }
    }

    private void SaveSettings()
    {
        try
        {
            var dict = _allSettings.ToDictionary(s => s.Key, s => s.Value);
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { /* ignore */ }
    }

    public string? GetSetting(string key)
    {
        return _allSettings.FirstOrDefault(s => s.Key == key)?.Value;
    }

    public void SetSetting(string key, string value)
    {
        var entry = _allSettings.FirstOrDefault(s => s.Key == key);
        if (entry != null)
        {
            entry.Value = value;
            SaveSettings();
        }
    }
}

public class SettingCategory
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
}

public partial class SettingEntry : ObservableObject
{
    public string Key { get; init; } = "";
    public string Category { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public SettingType Type { get; init; }
    public string DefaultValue { get; init; } = "";
    public List<string> EnumValues { get; init; } = [];

    [ObservableProperty]
    private string _value = "";

    public bool IsModified => Value != DefaultValue;
}

public enum SettingType
{
    String,
    Number,
    Boolean,
    Enum
}
