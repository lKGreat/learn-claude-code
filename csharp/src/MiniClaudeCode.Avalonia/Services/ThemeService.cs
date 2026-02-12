using Avalonia;
using Avalonia.Media;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Manages color theme switching at runtime by updating Application resources.
/// </summary>
public sealed class ThemeService
{
    public static ThemeService Instance { get; } = new();

    private readonly Dictionary<string, ThemeDefinition> _themes = new();

    public string CurrentTheme { get; private set; } = "catppuccin-mocha";
    public IReadOnlyList<ThemeDefinition> AvailableThemes => _themes.Values.ToList();
    public event Action<string>? ThemeChanged;

    private ThemeService()
    {
        RegisterBuiltInThemes();
    }

    public void Initialize()
    {
        var themeName = SettingsService.Instance.ColorTheme;
        ApplyTheme(themeName);
    }

    public void ApplyTheme(string themeName)
    {
        if (!_themes.TryGetValue(themeName, out var theme))
            theme = _themes["catppuccin-mocha"];

        var app = Application.Current;
        if (app == null) return;

        foreach (var (tokenName, hexColor) in theme.Colors)
        {
            var key = $"Theme{tokenName}";
            var color = Color.Parse(hexColor);
            app.Resources[key] = new SolidColorBrush(color);
        }

        CurrentTheme = theme.Name;
        SettingsService.Instance.Set("workbench.colorTheme", theme.Name);
        ThemeChanged?.Invoke(theme.Name);
    }

    private void RegisterBuiltInThemes()
    {
        _themes["catppuccin-mocha"] = new ThemeDefinition
        {
            Name = "catppuccin-mocha",
            DisplayName = "Catppuccin Mocha",
            IsDark = true,
            Colors = new Dictionary<string, string>
            {
                ["Base"] = "#1E1E2E", ["Mantle"] = "#181825", ["Crust"] = "#11111B",
                ["Surface0"] = "#313244", ["Surface1"] = "#45475A", ["Surface2"] = "#585B70",
                ["Overlay0"] = "#6C7086", ["Overlay1"] = "#7F849C",
                ["Subtext0"] = "#A6ADC8", ["Subtext1"] = "#BAC2DE", ["Text"] = "#CDD6F4",
                ["Blue"] = "#89B4FA", ["Green"] = "#A6E3A1", ["Red"] = "#F38BA8",
                ["Yellow"] = "#F9E2AF", ["Peach"] = "#FAB387", ["Mauve"] = "#CBA6F7", ["Teal"] = "#94E2D5"
            }
        };

        _themes["catppuccin-latte"] = new ThemeDefinition
        {
            Name = "catppuccin-latte",
            DisplayName = "Catppuccin Latte",
            IsDark = false,
            Colors = new Dictionary<string, string>
            {
                ["Base"] = "#EFF1F5", ["Mantle"] = "#E6E9EF", ["Crust"] = "#DCE0E8",
                ["Surface0"] = "#CCD0DA", ["Surface1"] = "#BCC0CC", ["Surface2"] = "#ACB0BE",
                ["Overlay0"] = "#9CA0B0", ["Overlay1"] = "#8C8FA1",
                ["Subtext0"] = "#6C6F85", ["Subtext1"] = "#5C5F77", ["Text"] = "#4C4F69",
                ["Blue"] = "#1E66F5", ["Green"] = "#40A02B", ["Red"] = "#D20F39",
                ["Yellow"] = "#DF8E1D", ["Peach"] = "#FE640B", ["Mauve"] = "#8839EF", ["Teal"] = "#179299"
            }
        };

        _themes["high-contrast"] = new ThemeDefinition
        {
            Name = "high-contrast",
            DisplayName = "High Contrast",
            IsDark = true,
            Colors = new Dictionary<string, string>
            {
                ["Base"] = "#000000", ["Mantle"] = "#0A0A0A", ["Crust"] = "#000000",
                ["Surface0"] = "#333333", ["Surface1"] = "#444444", ["Surface2"] = "#555555",
                ["Overlay0"] = "#888888", ["Overlay1"] = "#999999",
                ["Subtext0"] = "#BBBBBB", ["Subtext1"] = "#CCCCCC", ["Text"] = "#FFFFFF",
                ["Blue"] = "#6699FF", ["Green"] = "#66FF66", ["Red"] = "#FF6666",
                ["Yellow"] = "#FFFF66", ["Peach"] = "#FFAA66", ["Mauve"] = "#CC99FF", ["Teal"] = "#66FFFF"
            }
        };
    }
}
