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
            Colors = BuildMochaColors()
        };

        _themes["catppuccin-latte"] = new ThemeDefinition
        {
            Name = "catppuccin-latte",
            DisplayName = "Catppuccin Latte",
            IsDark = false,
            Colors = BuildLatteColors()
        };

        _themes["high-contrast"] = new ThemeDefinition
        {
            Name = "high-contrast",
            DisplayName = "High Contrast",
            IsDark = true,
            Colors = BuildHighContrastColors()
        };
    }

    private static Dictionary<string, string> BuildMochaColors() => new()
    {
        ["Base"] = "#1E1E2E", ["Mantle"] = "#181825", ["Crust"] = "#11111B",
        ["Surface0"] = "#313244", ["Surface1"] = "#45475A", ["Surface2"] = "#585B70",
        ["Overlay0"] = "#6C7086", ["Overlay1"] = "#7F849C",
        ["Subtext0"] = "#A6ADC8", ["Subtext1"] = "#BAC2DE", ["Text"] = "#CDD6F4",
        ["Blue"] = "#89B4FA", ["Green"] = "#A6E3A1", ["Red"] = "#F38BA8",
        ["Yellow"] = "#F9E2AF", ["Peach"] = "#FAB387", ["Mauve"] = "#CBA6F7", ["Teal"] = "#94E2D5",

        ["WorkbenchBackground"] = "#1E1E2E",
        ["TitleBarBackground"] = "#181825",
        ["TitleBarBorder"] = "#313244",
        ["ActivityBarBackground"] = "#181825",
        ["ActivityBarForeground"] = "#CDD6F4",
        ["ActivityBarInactiveForeground"] = "#7F849C",
        ["ActivityBarActiveBorder"] = "#89B4FA",
        ["SideBarBackground"] = "#181825",
        ["SideBarBorder"] = "#313244",
        ["EditorBackground"] = "#1E1E2E",
        ["PanelBackground"] = "#181825",
        ["PanelBorder"] = "#313244",
        ["StatusBarBackground"] = "#313244",
        ["StatusBarForeground"] = "#CDD6F4",
        ["StatusBarHoverBackground"] = "#45475A",
        ["InputBackground"] = "#11111B",
        ["InputForeground"] = "#CDD6F4",
        ["InputBorder"] = "#313244",
        ["InputFocusBorder"] = "#89B4FA",
        ["ButtonBackground"] = "#313244",
        ["ButtonForeground"] = "#CDD6F4",
        ["ButtonHoverBackground"] = "#45475A",
        ["Accent"] = "#89B4FA",
        ["AccentForeground"] = "#11111B",
        ["Danger"] = "#F38BA8",
        ["DangerForeground"] = "#11111B",
        ["ListHoverBackground"] = "#313244",
        ["ListActiveBackground"] = "#45475A",
        ["ListActiveForeground"] = "#CDD6F4",
        ["BadgeBackground"] = "#F38BA8",
        ["BadgeForeground"] = "#11111B",
        ["MutedForeground"] = "#A6ADC8",
        ["FocusBorder"] = "#89B4FA"
    };

    private static Dictionary<string, string> BuildLatteColors() => new()
    {
        ["Base"] = "#EFF1F5", ["Mantle"] = "#E6E9EF", ["Crust"] = "#DCE0E8",
        ["Surface0"] = "#CCD0DA", ["Surface1"] = "#BCC0CC", ["Surface2"] = "#ACB0BE",
        ["Overlay0"] = "#9CA0B0", ["Overlay1"] = "#8C8FA1",
        ["Subtext0"] = "#6C6F85", ["Subtext1"] = "#5C5F77", ["Text"] = "#4C4F69",
        ["Blue"] = "#1E66F5", ["Green"] = "#40A02B", ["Red"] = "#D20F39",
        ["Yellow"] = "#DF8E1D", ["Peach"] = "#FE640B", ["Mauve"] = "#8839EF", ["Teal"] = "#179299",

        ["WorkbenchBackground"] = "#EFF1F5",
        ["TitleBarBackground"] = "#E6E9EF",
        ["TitleBarBorder"] = "#CCD0DA",
        ["ActivityBarBackground"] = "#E6E9EF",
        ["ActivityBarForeground"] = "#4C4F69",
        ["ActivityBarInactiveForeground"] = "#8C8FA1",
        ["ActivityBarActiveBorder"] = "#1E66F5",
        ["SideBarBackground"] = "#E6E9EF",
        ["SideBarBorder"] = "#CCD0DA",
        ["EditorBackground"] = "#EFF1F5",
        ["PanelBackground"] = "#E6E9EF",
        ["PanelBorder"] = "#CCD0DA",
        ["StatusBarBackground"] = "#1E66F5",
        ["StatusBarForeground"] = "#FFFFFF",
        ["StatusBarHoverBackground"] = "#2D74F7",
        ["InputBackground"] = "#DCE0E8",
        ["InputForeground"] = "#4C4F69",
        ["InputBorder"] = "#BCC0CC",
        ["InputFocusBorder"] = "#1E66F5",
        ["ButtonBackground"] = "#CCD0DA",
        ["ButtonForeground"] = "#4C4F69",
        ["ButtonHoverBackground"] = "#BCC0CC",
        ["Accent"] = "#1E66F5",
        ["AccentForeground"] = "#FFFFFF",
        ["Danger"] = "#D20F39",
        ["DangerForeground"] = "#FFFFFF",
        ["ListHoverBackground"] = "#CCD0DA",
        ["ListActiveBackground"] = "#BCC0CC",
        ["ListActiveForeground"] = "#4C4F69",
        ["BadgeBackground"] = "#D20F39",
        ["BadgeForeground"] = "#FFFFFF",
        ["MutedForeground"] = "#6C6F85",
        ["FocusBorder"] = "#1E66F5"
    };

    private static Dictionary<string, string> BuildHighContrastColors() => new()
    {
        ["Base"] = "#000000", ["Mantle"] = "#0A0A0A", ["Crust"] = "#000000",
        ["Surface0"] = "#333333", ["Surface1"] = "#444444", ["Surface2"] = "#555555",
        ["Overlay0"] = "#888888", ["Overlay1"] = "#999999",
        ["Subtext0"] = "#BBBBBB", ["Subtext1"] = "#CCCCCC", ["Text"] = "#FFFFFF",
        ["Blue"] = "#6699FF", ["Green"] = "#66FF66", ["Red"] = "#FF6666",
        ["Yellow"] = "#FFFF66", ["Peach"] = "#FFAA66", ["Mauve"] = "#CC99FF", ["Teal"] = "#66FFFF",

        ["WorkbenchBackground"] = "#000000",
        ["TitleBarBackground"] = "#0A0A0A",
        ["TitleBarBorder"] = "#FFFFFF",
        ["ActivityBarBackground"] = "#000000",
        ["ActivityBarForeground"] = "#FFFFFF",
        ["ActivityBarInactiveForeground"] = "#BBBBBB",
        ["ActivityBarActiveBorder"] = "#6699FF",
        ["SideBarBackground"] = "#0A0A0A",
        ["SideBarBorder"] = "#FFFFFF",
        ["EditorBackground"] = "#000000",
        ["PanelBackground"] = "#0A0A0A",
        ["PanelBorder"] = "#FFFFFF",
        ["StatusBarBackground"] = "#333333",
        ["StatusBarForeground"] = "#FFFFFF",
        ["StatusBarHoverBackground"] = "#444444",
        ["InputBackground"] = "#000000",
        ["InputForeground"] = "#FFFFFF",
        ["InputBorder"] = "#FFFFFF",
        ["InputFocusBorder"] = "#6699FF",
        ["ButtonBackground"] = "#333333",
        ["ButtonForeground"] = "#FFFFFF",
        ["ButtonHoverBackground"] = "#444444",
        ["Accent"] = "#6699FF",
        ["AccentForeground"] = "#000000",
        ["Danger"] = "#FF6666",
        ["DangerForeground"] = "#000000",
        ["ListHoverBackground"] = "#333333",
        ["ListActiveBackground"] = "#444444",
        ["ListActiveForeground"] = "#FFFFFF",
        ["BadgeBackground"] = "#FF6666",
        ["BadgeForeground"] = "#000000",
        ["MutedForeground"] = "#CCCCCC",
        ["FocusBorder"] = "#6699FF"
    };
}
