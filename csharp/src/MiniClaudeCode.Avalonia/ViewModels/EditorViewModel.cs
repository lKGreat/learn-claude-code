using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Editor;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.Services.Explorer;
using MiniClaudeCode.Core.AI;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Result of a save confirmation dialog.
/// </summary>
public enum SaveConfirmResult { Save, DontSave, Cancel }

/// <summary>
/// ViewModel for the editor area - manages open tabs, editor groups, and split view.
/// Integrates with ITextFileService for file operations (doc sections 6, 7, 8).
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

    // =========================================================================
    // Save Confirmation Support
    // =========================================================================

    /// <summary>Fired when user tries to close a dirty tab. Returns Save/DontSave/Cancel.</summary>
    public event Func<EditorTab, Task<SaveConfirmResult>>? SaveConfirmRequested;

    /// <summary>Fired when a save error occurs (for notification).</summary>
    public event Action<string>? SaveError;

    // =========================================================================
    // External File Change Detection
    // =========================================================================

    /// <summary>Fired when a conflict is detected (dirty file changed on disk). Returns user choice.</summary>
    public event Func<EditorTab, string, Task<string?>>? ConflictResolutionRequested;

    // =========================================================================
    // Inline Completion Support
    // =========================================================================

    private InlineCompletionService? _completionService;
    private CancellationTokenSource? _completionCts;
    private const int CompletionDebounceMs = 800;

    /// <summary>Ghost text to display as inline completion suggestion.</summary>
    [ObservableProperty]
    private string? _ghostText;

    /// <summary>Loading indicator for completion requests.</summary>
    [ObservableProperty]
    private bool _isCompletionLoading;

    /// <summary>The offset in the current line where ghost text should be displayed.</summary>
    [ObservableProperty]
    private int _ghostTextColumn;

    // =========================================================================
    // Advanced Editor Features
    // =========================================================================

    /// <summary>Whether to show the minimap at the right edge of the editor.</summary>
    [ObservableProperty]
    private bool _isMinimapVisible = true;

    /// <summary>Whether code folding is enabled.</summary>
    [ObservableProperty]
    private bool _isFoldingEnabled = true;

    /// <summary>Whether indent guide lines are visible.</summary>
    [ObservableProperty]
    private bool _isIndentGuidesVisible = true;

    /// <summary>Breadcrumb navigation ViewModel.</summary>
    public BreadcrumbViewModel BreadcrumbNav { get; } = new();

    // =========================================================================
    // Inline Edit Support (Ctrl+K)
    // =========================================================================

    /// <summary>Inline edit panel ViewModel.</summary>
    public InlineEditViewModel InlineEdit { get; } = new();

    /// <summary>Find and Replace panel ViewModel.</summary>
    public FindReplaceViewModel FindReplace { get; } = new();

    /// <summary>Fired when inline edit is accepted (for applying changes).</summary>
    public event Action<string, string>? InlineEditAccepted; // (filePath, modifiedCode)

    // =========================================================================
    // TextFileService Integration (Doc Sections 6, 7, 8)
    // =========================================================================

    private ITextFileService? _textFileService;
    private FileBackupService? _backupService;

    /// <summary>Set the text file service instance (called from MainWindowViewModel).</summary>
    public void SetTextFileService(ITextFileService service)
    {
        _textFileService = service;

        // Subscribe to service events
        _textFileService.OnDidChangeDirty += OnModelDirtyChanged;
        _textFileService.OnDidSave += OnModelSaved;
        _textFileService.OnDidLoad += OnModelLoaded;
    }

    /// <summary>Set the backup service instance (called from MainWindowViewModel).</summary>
    public void SetBackupService(FileBackupService service) => _backupService = service;

    private void OnModelDirtyChanged(TextFileModelChangeEvent evt)
    {
        var tab = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, evt.Resource, StringComparison.OrdinalIgnoreCase));
        if (tab != null)
        {
            tab.IsDirty = evt.IsDirty;
        }
    }

    private void OnModelSaved(TextFileSaveEvent evt)
    {
        var tab = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, evt.Resource, StringComparison.OrdinalIgnoreCase));
        if (tab != null)
        {
            tab.MarkSaved();
        }
    }

    private void OnModelLoaded(TextFileLoadEvent evt)
    {
        // Model loaded -- could be used for notifications
    }

    public EditorViewModel()
    {
        // Wire up inline edit events
        InlineEdit.EditAccepted += OnInlineEditAccepted;
    }

    private void OnInlineEditAccepted(string filePath, string modifiedCode)
    {
        // Notify the view to apply the edit
        InlineEditAccepted?.Invoke(filePath, modifiedCode);
    }

    /// <summary>Set the completion service instance (called from MainWindowViewModel).</summary>
    public void SetCompletionService(InlineCompletionService? service)
    {
        _completionService = service;
    }

    // =========================================================================
    // Inline Completion
    // =========================================================================

    public async Task RequestCompletionAsync(string text, int line, int column)
    {
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        DismissCompletion();

        if (_completionService == null || ActiveTab == null)
            return;

        if (IsLargeFile)
            return;

        try
        {
            await Task.Delay(CompletionDebounceMs, _completionCts.Token);
            IsCompletionLoading = true;

            var lines = text.Split('\n');
            var beforeCursor = string.Join('\n', lines.Take(line));
            var afterCursor = line < lines.Length
                ? string.Join('\n', lines.Skip(line))
                : "";

            var request = new CompletionRequest
            {
                FilePath = ActiveTab.FilePath,
                Language = ActiveTab.Language,
                Line = line,
                Column = column,
                CodeBefore = beforeCursor,
                CodeAfter = afterCursor
            };

            var result = await _completionService.GetCompletionAsync(request, _completionCts.Token);

            if (result != null && !string.IsNullOrWhiteSpace(result.Text))
            {
                GhostText = result.Text;
                GhostTextColumn = column;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Completion error: {ex.Message}");
        }
        finally
        {
            IsCompletionLoading = false;
        }
    }

    public string? AcceptCompletion()
    {
        if (string.IsNullOrEmpty(GhostText))
            return null;

        var text = GhostText;
        DismissCompletion();
        return text;
    }

    public void DismissCompletion()
    {
        GhostText = null;
        GhostTextColumn = 0;
    }

    public void CancelPendingCompletion()
    {
        _completionCts?.Cancel();
        _completionCts = null;
        DismissCompletion();
    }

    // =========================================================================
    // Inline Edit (Ctrl+K)
    // =========================================================================

    public void ShowInlineEdit(
        string selectedCode,
        int startLine,
        int endLine,
        string contextBefore,
        string contextAfter)
    {
        if (ActiveTab == null || string.IsNullOrWhiteSpace(selectedCode))
            return;

        InlineEdit.Show(
            ActiveTab.FilePath,
            ActiveTab.Language,
            selectedCode,
            startLine,
            endLine,
            contextBefore,
            contextAfter);
    }

    // =========================================================================
    // File Open / Tab Management (Doc Section 6)
    // =========================================================================

    /// <summary>
    /// Create a tab from a file path, using TextFileService for model caching.
    /// </summary>
    private async Task<EditorTab> CreateTabAsync(string filePath, bool isPreview = false)
    {
        DebugLogger.Log($"CreateTabAsync: {filePath}, isPreview={isPreview}");
        
        string content;
        PieceTreeTextBuffer? textBuffer = null;
        TextFileModel? model = null;

        try
        {
            if (_textFileService != null)
            {
                DebugLogger.Log($"Using TextFileService to resolve: {filePath}");
                // Use TextFileService for model caching (doc 6.3)
                model = await _textFileService.ResolveAsync(filePath);
                content = model.Content;
                textBuffer = model.TextBuffer;
                DebugLogger.Log($"File resolved, length={content.Length}");
            }
            else
            {
                DebugLogger.Log($"Fallback: direct file read");
                // Fallback: direct file read
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    if (fileInfo.Length > LargeFileConstants.LargeFileSizeThreshold)
                    {
                        textBuffer = await PieceTreeTextBufferBuilder.LoadFileAsync(filePath);
                        var viewportLines = 200;
                        var sb = new System.Text.StringBuilder();
                        for (int line = 1; line <= Math.Min(viewportLines, textBuffer.LineCount); line++)
                        {
                            if (line > 1) sb.Append('\n');
                            sb.Append(textBuffer.GetLineContent(line));
                        }
                        content = sb.ToString();
                        DebugLogger.Log($"Large file loaded via PieceTree, lineCount={textBuffer.LineCount}");
                    }
                    else
                    {
                        content = File.ReadAllText(filePath);
                        DebugLogger.Log($"File read, length={content.Length}");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogError($"Error loading file content: {filePath}", ex);
                    content = $"Error loading file: {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Error loading file content: {filePath}", ex);
            content = $"Error loading file: {ex.Message}";
        }

        var tab = new EditorTab
        {
            FilePath = filePath,
            Content = content,
            TextBuffer = textBuffer,
            IsPreview = isPreview,
            Model = model,
        };

        // Store disk metadata for external change detection (doc 5.4)
        try
        {
            var fi = new FileInfo(filePath);
            tab.UpdateDiskMetadata(content, fi.LastWriteTimeUtc);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Failed to update disk metadata for: {filePath}", ex);
        }

        DebugLogger.Log($"CreateTabAsync completed for: {filePath}");
        return tab;
    }

    /// <summary>
    /// Open a file in the editor (permanent tab).
    /// Follows doc section 6.1 flow: check existing -> resolve -> create/activate tab.
    /// </summary>
    public async void OpenFile(string filePath)
    {
        DebugLogger.Log($"OpenFile called: {filePath}");
        
        try
        {
            // Step 1: Check if already open (doc 6.2 step 1)
            var existing = Tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing != null)
            {
                DebugLogger.Log($"File already open, activating tab");
                // If it's a preview tab, pin it (make permanent)
                if (existing.IsPreview)
                    existing.IsPreview = false;
                ActivateTab(existing);
                return;
            }

            DebugLogger.Log($"Creating new tab for: {filePath}");
            // Step 2-5: Create tab with model (doc 6.2 steps 2-5)
            var tab = await CreateTabAsync(filePath, isPreview: false);

            DebugLogger.Log($"Tab created, adding to list");
            // Step 6: Add to tab list
            Tabs.Add(tab);
            ActivateTab(tab);

            // Step 7: Update large file warning
            if (tab.IsLargeFile)
            {
                IsLargeFile = true;
                try
                {
                    LargeFileWarning = $"Large file ({new FileInfo(filePath).Length / (1024 * 1024):F1} MB, {tab.TextBuffer!.LineCount:N0} lines) - Some features disabled for performance.";
                }
                catch
                {
                    LargeFileWarning = "Large file - Some features disabled for performance.";
                }
            }
            else
            {
                IsLargeFile = false;
                LargeFileWarning = "";
            }
            
            DebugLogger.Log($"OpenFile completed successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Failed to open file: {filePath}", ex);
            SaveError?.Invoke($"Failed to open file: {ex.Message}");
        }
    }

    /// <summary>
    /// Open a file in preview mode (single-click). Preview tabs are replaced by the next preview.
    /// Follows doc section 6.2 preview handling.
    /// </summary>
    public async void PreviewFile(string filePath)
    {
        DebugLogger.Log($"PreviewFile called: {filePath}");
        
        try
        {
            // Check if already open (preview or permanent)
            var existing = Tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing != null)
            {
                DebugLogger.Log($"File already open, activating existing tab");
                ActivateTab(existing);
                return;
            }

            // Find and replace existing preview tab (doc 6.2 step 2 - preview mode)
            var existingPreview = Tabs.FirstOrDefault(t => t.IsPreview);
            if (existingPreview != null)
            {
                DebugLogger.Log($"Replacing existing preview tab");
                var index = Tabs.IndexOf(existingPreview);

                // Release model reference for old preview tab
                if (_textFileService != null && existingPreview.Model != null)
                    _textFileService.ReleaseModel(existingPreview.FilePath);

                Tabs.Remove(existingPreview);

                var tab = await CreateTabAsync(filePath, isPreview: true);
                Tabs.Insert(index, tab);
                ActivateTab(tab);
            }
            else
            {
                DebugLogger.Log($"Creating new preview tab");
                var tab = await CreateTabAsync(filePath, isPreview: true);
                Tabs.Add(tab);
                ActivateTab(tab);
            }

            IsLargeFile = false;
            LargeFileWarning = "";
            
            DebugLogger.Log($"PreviewFile completed successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Failed to preview file: {filePath}", ex);
            SaveError?.Invoke($"Failed to preview file: {ex.Message}");
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

        // Step 8: Fire events (doc 6.1 step 8)
        ActiveFileChanged?.Invoke(tab);
    }

    // =========================================================================
    // Close Tab (Doc Section 8)
    // =========================================================================

    [RelayCommand]
    private async Task CloseTab(EditorTab? tab)
    {
        if (tab == null) return;

        // Step 2: Check dirty state (doc 8.1 step 2)
        if (tab.IsDirty)
        {
            if (SaveConfirmRequested != null)
            {
                var result = await SaveConfirmRequested.Invoke(tab);
                switch (result)
                {
                    case SaveConfirmResult.Save:
                        await SaveFileForTabAsync(tab);
                        break;
                    case SaveConfirmResult.Cancel:
                        return; // Don't close
                    case SaveConfirmResult.DontSave:
                        break; // Close without saving
                }
            }
        }

        var index = Tabs.IndexOf(tab);
        var wasActive = tab == ActiveTab;

        // Step 3: Remove from tab list (doc 8.1 step 3)
        Tabs.Remove(tab);

        // Step 4: Release model reference (doc 8.1 step 4)
        if (_textFileService != null && tab.Model != null)
            _textFileService.ReleaseModel(tab.FilePath);

        // Step 5: Fire close event and activate next tab (doc 8.1 step 5)
        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            CurrentContent = "";
            HasOpenFiles = false;
            Breadcrumb = "";
            ActiveFileChanged?.Invoke(null);
        }
        else if (wasActive)
        {
            var nextIndex = Math.Min(index, Tabs.Count - 1);
            ActivateTab(Tabs[nextIndex]);
        }
    }

    [RelayCommand]
    private async Task CloseOtherTabs(EditorTab? tab)
    {
        if (tab == null) return;
        var toRemove = Tabs.Where(t => t != tab && !t.IsPinned).ToList();
        foreach (var t in toRemove)
        {
            await CloseTab(t);
            // If user cancelled on any tab, stop
            if (Tabs.Contains(t)) return;
        }
        if (Tabs.Contains(tab))
            ActivateTab(tab);
    }

    [RelayCommand]
    private async Task CloseAllTabs()
    {
        var allTabs = Tabs.ToList();
        foreach (var t in allTabs)
        {
            await CloseTab(t);
            // If user cancelled, stop closing
            if (Tabs.Contains(t)) return;
        }
    }

    [RelayCommand]
    private void PinTab(EditorTab? tab)
    {
        if (tab != null) tab.IsPinned = !tab.IsPinned;
    }

    // =========================================================================
    // Save (Doc Section 7)
    // =========================================================================

    /// <summary>
    /// Save a specific tab to disk using TextFileService.
    /// Follows doc section 7.2 flow: onWillSave -> backup -> write -> update -> onDidSave.
    /// </summary>
    public async Task SaveFileForTabAsync(EditorTab tab)
    {
        if (_textFileService != null)
        {
            // Sync content from tab to model before saving
            var model = _textFileService.GetModel(tab.FilePath);
            if (model != null && model.Content != tab.Content)
            {
                model.ApplyEdit(tab.Content);
            }

            var success = await _textFileService.SaveAsync(tab.FilePath, new SaveOptions
            {
                CreateBackup = true,
                Reason = SaveReason.Explicit
            });

            if (!success)
            {
                SaveError?.Invoke($"Failed to save {tab.FileName}");
            }
        }
        else
        {
            // Fallback: direct save
            SaveFileForTab(tab);
        }
    }

    /// <summary>Save a specific tab to disk (synchronous fallback).</summary>
    public void SaveFileForTab(EditorTab tab)
    {
        try
        {
            _backupService?.CreateBackup(tab.FilePath);

            if (tab.TextBuffer != null)
            {
                using var writer = new StreamWriter(tab.FilePath);
                foreach (var chunk in tab.TextBuffer.CreateSnapshot())
                    writer.Write(chunk);
            }
            else
            {
                File.WriteAllText(tab.FilePath, tab.Content);
            }
            tab.MarkSaved();

            // Update disk metadata (doc 5.4)
            try
            {
                var fi = new FileInfo(tab.FilePath);
                tab.UpdateDiskMetadata(tab.Content, fi.LastWriteTimeUtc);
            }
            catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            SaveError?.Invoke($"Failed to save {tab.FileName}: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (ActiveTab == null || !ActiveTab.IsDirty) return;

        if (_textFileService != null)
        {
            _ = SaveFileForTabAsync(ActiveTab);
        }
        else
        {
            SaveFileForTab(ActiveTab);
        }
    }

    /// <summary>
    /// Update content from editor. Uses version-based dirty detection (doc 5.3).
    /// </summary>
    public void UpdateContent(string newContent)
    {
        if (ActiveTab == null) return;

        // Auto-pin preview tab when user starts editing
        if (ActiveTab.IsPreview)
            ActiveTab.IsPreview = false;

        ActiveTab.Content = newContent;
        ActiveTab.IncrementVersion(); // Version-based dirty detection (doc 5.3)
        CurrentContent = newContent;

        // Also update the TextFileModel if available
        if (ActiveTab.Model != null)
        {
            ActiveTab.Model.ApplyEdit(newContent);
        }
    }

    public void UpdateCursorPosition(int line, int column)
    {
        CursorLine = line;
        CursorColumn = column;
        CursorPositionChanged?.Invoke(line, column);
    }

    // =========================================================================
    // External File Change Detection (Doc Section 5.4)
    // =========================================================================

    /// <summary>
    /// Handle an externally modified file that is open in a tab.
    /// Uses hash-based comparison for accurate conflict detection.
    /// </summary>
    public void HandleExternalFileChange(EditorTab tab)
    {
        if (!File.Exists(tab.FilePath))
        {
            HandleExternalFileDeletion(tab);
            return;
        }

        try
        {
            var fi = new FileInfo(tab.FilePath);
            var diskContent = File.ReadAllText(tab.FilePath);

            // Use hash-based comparison (doc 5.4)
            if (!tab.HasDiskContentChanged(diskContent, fi.LastWriteTimeUtc))
                return;

            if (!tab.IsDirty)
            {
                // File is clean: silently reload (doc 5.4)
                tab.Content = diskContent;
                tab.IsDirty = false;
                tab.FileState = FileState.Saved;
                tab.UpdateDiskMetadata(diskContent, fi.LastWriteTimeUtc);

                // Also update the TextFileModel
                if (tab.Model != null)
                {
                    tab.Model.Revert(diskContent);
                    tab.Model.UpdateDiskMetadata(diskContent, fi.LastWriteTimeUtc);
                }

                if (tab == ActiveTab)
                {
                    CurrentContent = diskContent;
                    ActiveFileChanged?.Invoke(tab);
                }
            }
            else
            {
                // File is dirty: conflict! (doc 5.4)
                tab.FileState = FileState.Conflict;
                _ = ResolveConflictAsync(tab, diskContent, fi.LastWriteTimeUtc);
            }
        }
        catch (Exception ex)
        {
            tab.FileState = FileState.Error;
            SaveError?.Invoke($"Error reading {tab.FileName}: {ex.Message}");
        }
    }

    private async Task ResolveConflictAsync(EditorTab tab, string diskContent, DateTime diskModTime)
    {
        if (ConflictResolutionRequested != null)
        {
            var result = await ConflictResolutionRequested.Invoke(tab,
                $"'{tab.FileName}' has been changed on disk. Overwrite with disk version?");

            if (result == "Overwrite")
            {
                tab.Content = diskContent;
                tab.IsDirty = false;
                tab.FileState = FileState.Saved;
                tab.UpdateDiskMetadata(diskContent, diskModTime);

                // Also update the TextFileModel
                if (tab.Model != null)
                {
                    tab.Model.Revert(diskContent);
                    tab.Model.UpdateDiskMetadata(diskContent, diskModTime);
                }

                if (tab == ActiveTab)
                {
                    CurrentContent = diskContent;
                    ActiveFileChanged?.Invoke(tab);
                }
            }
            // else: keep local changes, stay in Conflict state
        }
    }

    /// <summary>Handle an externally deleted file that is open in a tab (doc 5.4).</summary>
    public void HandleExternalFileDeletion(EditorTab tab)
    {
        tab.FileState = FileState.Orphan;
        if (!tab.IsDirty)
            tab.IsDirty = true; // Mark dirty so save-on-close prompts

        if (tab.Model != null)
            tab.Model.IsOrphaned = true;
    }

    /// <summary>Handle an externally renamed file that is open in a tab.</summary>
    public async void HandleExternalFileRename(EditorTab tab, string newPath)
    {
        // Since FilePath is init-only, we close old tab and open new one at same position
        var index = Tabs.IndexOf(tab);
        var wasActive = tab == ActiveTab;
        var oldContent = tab.Content;
        var wasDirty = tab.IsDirty;

        // Release old model reference
        if (_textFileService != null && tab.Model != null)
            _textFileService.ReleaseModel(tab.FilePath);

        Tabs.Remove(tab);

        var newTab = await CreateTabAsync(newPath);
        if (wasDirty)
        {
            newTab.Content = oldContent;
            newTab.IsDirty = true;
        }

        if (index >= 0 && index <= Tabs.Count)
            Tabs.Insert(index, newTab);
        else
            Tabs.Add(newTab);

        if (wasActive)
            ActivateTab(newTab);
    }

    // =========================================================================
    // Navigation
    // =========================================================================

    public event Action<int>? GoToLineRequested;

    public void GoToLine(int line)
    {
        if (line < 1 || ActiveTab == null) return;
        GoToLineRequested?.Invoke(line);
    }

    // =========================================================================
    // Diff Support
    // =========================================================================

    public event Action<string, string, string>? DiffOpenRequested;

    public void OpenDiff(string fullPath, string relativePath)
    {
        OpenFile(fullPath);
        _ = LoadAndShowDiffAsync(fullPath, relativePath);
    }

    private async Task LoadAndShowDiffAsync(string fullPath, string relativePath)
    {
        try
        {
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

    // =========================================================================
    // Split Editor
    // =========================================================================

    [RelayCommand]
    private void SplitEditor()
    {
        if (ActiveTab == null) return;

        if (!IsSplit)
        {
            var group1 = new EditorGroup { GroupId = _nextGroupId++, IsActive = false };
            foreach (var tab in Tabs) group1.Tabs.Add(tab);
            if (ActiveTab != null) group1.ActivateTab(ActiveTab);

            var group2 = new EditorGroup { GroupId = _nextGroupId++, IsActive = true };

            Groups.Clear();
            Groups.Add(group1);
            Groups.Add(group2);
            ActiveGroup = group2;
            IsSplit = true;
        }
        else
        {
            var newGroup = new EditorGroup { GroupId = _nextGroupId++, IsActive = true };
            if (ActiveGroup != null) ActiveGroup.IsActive = false;
            Groups.Add(newGroup);
            ActiveGroup = newGroup;
        }
    }

    [RelayCommand]
    private void UnsplitEditor()
    {
        if (!IsSplit) return;

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

    // =========================================================================
    // Find & Replace
    // =========================================================================

    public void ShowFind()
    {
        FindReplace.IsVisible = true;
        FindReplace.IsReplaceVisible = false;
    }

    public void ShowReplace()
    {
        FindReplace.IsVisible = true;
        FindReplace.IsReplaceVisible = true;
    }

    // =========================================================================
    // Breadcrumb
    // =========================================================================

    private string _workspacePath = string.Empty;

    public void SetWorkspacePath(string path) => _workspacePath = path;

    private void UpdateBreadcrumb(EditorTab tab)
    {
        var dir = Path.GetDirectoryName(tab.FilePath) ?? "";
        var segments = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var last3 = segments.Length > 3 ? segments[^3..] : segments;
        Breadcrumb = string.Join(" > ", last3.Append(tab.FileName));
        BreadcrumbNav.Update(tab.FilePath, _workspacePath);
    }
}
