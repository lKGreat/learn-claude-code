using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Editor;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the editor area - manages open tabs, editor groups, and split view.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    public ObservableCollection<EditorTab> Tabs { get; } = [];
    public ObservableCollection<EditorGroup> Groups { get; } = [];

    [ObservableProperty]
    private EditorTab? _activeTab;

    [ObservableProperty]
    private string _currentContent = "";

    [ObservableProperty]
    private bool _hasOpenFiles;

    [ObservableProperty]
    private string _breadcrumb = "";

    [ObservableProperty]
    private bool _isLargeFile;

    [ObservableProperty]
    private bool _isSplit;

    [ObservableProperty]
    private EditorGroup? _activeGroup;

    private int _nextGroupId = 1;

    [ObservableProperty]
    private string _largeFileWarning = "";

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

        // Load file content using chunked reading for large file support
        string content;
        PieceTreeTextBuffer? textBuffer = null;
        bool isLarge = false;
        try
        {
            var fileInfo = new FileInfo(filePath);
            
            if (fileInfo.Length > LargeFileConstants.LargeFileSizeThreshold)
            {
                // Load via PieceTreeTextBuffer for large files - keep the buffer reference
                isLarge = true;
                textBuffer = PieceTreeTextBufferBuilder.LoadFileAsync(filePath).GetAwaiter().GetResult();
                
                // Only load first viewport worth of content (avoid materializing entire file)
                var viewportLines = 200;
                var sb = new System.Text.StringBuilder();
                for (int line = 1; line <= Math.Min(viewportLines, textBuffer.LineCount); line++)
                {
                    if (line > 1) sb.Append('\n');
                    sb.Append(textBuffer.GetLineContent(line));
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
            TextBuffer = textBuffer,
        };

        Tabs.Add(tab);
        ActivateTab(tab);
        
        if (isLarge)
        {
            IsLargeFile = true;
            LargeFileWarning = $"Large file ({new FileInfo(filePath).Length / (1024 * 1024):F1} MB, {textBuffer!.LineCount:N0} lines) - Some features disabled for performance.";
        }
        else
        {
            IsLargeFile = false;
            LargeFileWarning = "";
        }
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

    /// <summary>Fired when a save error occurs (for notification).</summary>
    public event Action<string>? SaveError;

    [RelayCommand]
    private void SaveFile()
    {
        if (ActiveTab == null || !ActiveTab.IsDirty) return;

        try
        {
            if (ActiveTab.TextBuffer != null)
            {
                // Large file: save from PieceTree buffer
                using var writer = new StreamWriter(ActiveTab.FilePath);
                foreach (var chunk in ActiveTab.TextBuffer.CreateSnapshot())
                {
                    writer.Write(chunk);
                }
            }
            else
            {
                File.WriteAllText(ActiveTab.FilePath, ActiveTab.Content);
            }
            ActiveTab.IsDirty = false;
        }
        catch (Exception ex)
        {
            SaveError?.Invoke($"Failed to save {ActiveTab.FileName}: {ex.Message}");
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

    /// <summary>
    /// Request to scroll the editor to a specific line.
    /// The view should subscribe to GoToLineRequested to perform the actual scroll.
    /// </summary>
    public event Action<int>? GoToLineRequested;

    /// <summary>Navigate to a specific line in the active editor.</summary>
    public void GoToLine(int line)
    {
        if (line < 1 || ActiveTab == null) return;
        GoToLineRequested?.Invoke(line);
    }

    /// <summary>
    /// Open a file in diff mode (showing git changes).
    /// Fires DiffOpenRequested for the view to render.
    /// </summary>
    public event Action<string, string, string>? DiffOpenRequested;

    /// <summary>Open a file showing its git diff.</summary>
    public void OpenDiff(string fullPath, string relativePath)
    {
        // Open the file in a tab first
        OpenFile(fullPath);

        // Then request the diff view
        _ = LoadAndShowDiffAsync(fullPath, relativePath);
    }

    private async Task LoadAndShowDiffAsync(string fullPath, string relativePath)
    {
        try
        {
            // Get diff from git
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff \"{relativePath}\"",
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? "",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return;

            var diff = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(diff))
            {
                DiffOpenRequested?.Invoke(fullPath, relativePath, diff);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load diff for {relativePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Split the editor into two groups. Moves the active tab to the new group.
    /// </summary>
    [RelayCommand]
    private void SplitEditor()
    {
        if (ActiveTab == null) return;

        if (!IsSplit)
        {
            // Create group 1 from existing tabs
            var group1 = new EditorGroup { GroupId = _nextGroupId++, IsActive = false };
            foreach (var tab in Tabs) group1.Tabs.Add(tab);
            if (ActiveTab != null) group1.ActivateTab(ActiveTab);

            // Create group 2 (empty for now - will open same file)
            var group2 = new EditorGroup { GroupId = _nextGroupId++, IsActive = true };

            Groups.Clear();
            Groups.Add(group1);
            Groups.Add(group2);
            ActiveGroup = group2;
            IsSplit = true;
        }
        else
        {
            // Already split - add another group
            var newGroup = new EditorGroup { GroupId = _nextGroupId++, IsActive = true };

            // Deactivate current active group
            if (ActiveGroup != null) ActiveGroup.IsActive = false;

            Groups.Add(newGroup);
            ActiveGroup = newGroup;
        }
    }

    /// <summary>
    /// Close all split groups and merge back to single editor.
    /// </summary>
    [RelayCommand]
    private void UnsplitEditor()
    {
        if (!IsSplit) return;

        // Collect all unique tabs from all groups
        var allTabs = Groups.SelectMany(g => g.Tabs)
            .GroupBy(t => t.FilePath)
            .Select(g => g.First())
            .ToList();

        Groups.Clear();
        IsSplit = false;
        ActiveGroup = null;

        Tabs.Clear();
        foreach (var tab in allTabs) Tabs.Add(tab);

        if (Tabs.Count > 0)
            ActivateTab(Tabs[0]);
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
