using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the editor area - manages open tabs and editor groups.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    public ObservableCollection<EditorTab> Tabs { get; } = [];

    [ObservableProperty]
    private EditorTab? _activeTab;

    [ObservableProperty]
    private string _currentContent = "";

    [ObservableProperty]
    private bool _hasOpenFiles;

    [ObservableProperty]
    private string _breadcrumb = "";

    // Cursor tracking for status bar
    [ObservableProperty]
    private int _cursorLine = 1;

    [ObservableProperty]
    private int _cursorColumn = 1;

    /// <summary>Fired when cursor position changes (for status bar).</summary>
    public event Action<int, int>? CursorPositionChanged;

    /// <summary>Fired when the active file changes (for status bar language).</summary>
    public event Action<EditorTab?>? ActiveFileChanged;

    public void OpenFile(string filePath)
    {
        // Check if already open
        var existing = Tabs.FirstOrDefault(t => t.FilePath == filePath);
        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }

        // Load file content
        string content;
        try
        {
            var fileInfo = new FileInfo(filePath);
            // Large file check: if > 20 MB, load with warning
            if (fileInfo.Length > 20 * 1024 * 1024)
            {
                content = $"[Large file: {fileInfo.Length / (1024 * 1024):F1} MB - Some features disabled for performance]\n\n";
                // Read in chunks for large files
                using var reader = new StreamReader(filePath);
                var buffer = new char[65535];
                int read;
                var sb = new System.Text.StringBuilder(content);
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sb.Append(buffer, 0, read);
                }
                content = sb.ToString();
            }
            else
            {
                content = File.ReadAllText(filePath);
            }
        }
        catch (Exception ex)
        {
            content = $"Error loading file: {ex.Message}";
        }

        var tab = new EditorTab
        {
            FilePath = filePath,
            Content = content,
        };

        Tabs.Add(tab);
        ActivateTab(tab);
    }

    public void ActivateTab(EditorTab tab)
    {
        if (ActiveTab != null) ActiveTab.IsActive = false;
        tab.IsActive = true;
        ActiveTab = tab;
        CurrentContent = tab.Content;
        HasOpenFiles = true;
        UpdateBreadcrumb(tab);
        ActiveFileChanged?.Invoke(tab);
    }

    [RelayCommand]
    private void CloseTab(EditorTab? tab)
    {
        if (tab == null) return;

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            CurrentContent = "";
            HasOpenFiles = false;
            Breadcrumb = "";
            ActiveFileChanged?.Invoke(null);
        }
        else if (tab == ActiveTab)
        {
            // Activate adjacent tab
            var nextIndex = Math.Min(index, Tabs.Count - 1);
            ActivateTab(Tabs[nextIndex]);
        }
    }

    [RelayCommand]
    private void CloseOtherTabs(EditorTab? tab)
    {
        if (tab == null) return;
        var toRemove = Tabs.Where(t => t != tab && !t.IsPinned).ToList();
        foreach (var t in toRemove) Tabs.Remove(t);
        ActivateTab(tab);
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        Tabs.Clear();
        ActiveTab = null;
        CurrentContent = "";
        HasOpenFiles = false;
        Breadcrumb = "";
        ActiveFileChanged?.Invoke(null);
    }

    [RelayCommand]
    private void PinTab(EditorTab? tab)
    {
        if (tab != null) tab.IsPinned = !tab.IsPinned;
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (ActiveTab == null || !ActiveTab.IsDirty) return;

        try
        {
            File.WriteAllText(ActiveTab.FilePath, ActiveTab.Content);
            ActiveTab.IsDirty = false;
        }
        catch
        {
            // Handle save errors
        }
    }

    public void UpdateContent(string newContent)
    {
        if (ActiveTab == null) return;
        ActiveTab.Content = newContent;
        ActiveTab.IsDirty = true;
        CurrentContent = newContent;
    }

    public void UpdateCursorPosition(int line, int column)
    {
        CursorLine = line;
        CursorColumn = column;
        CursorPositionChanged?.Invoke(line, column);
    }

    private void UpdateBreadcrumb(EditorTab tab)
    {
        // Build breadcrumb: directory > filename
        var dir = Path.GetDirectoryName(tab.FilePath) ?? "";
        var segments = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var last3 = segments.Length > 3 ? segments[^3..] : segments;
        Breadcrumb = string.Join(" > ", last3.Append(tab.FileName));
    }
}
