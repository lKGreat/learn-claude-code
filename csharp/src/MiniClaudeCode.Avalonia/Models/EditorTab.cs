using CommunityToolkit.Mvvm.ComponentModel;
using AvaloniaEdit.Document;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// File state tracking for open editor tabs.
/// Maps to VSCode's FileState (Section 5.1).
/// </summary>
public enum FileState
{
    Saved,      // File matches disk
    Dirty,      // Unsaved local changes
    Conflict,   // File changed on disk AND has local changes
    Orphan,     // File deleted from disk while open
    Error       // Failed to read/write
}

/// <summary>
/// Represents an open file tab in the editor area.
/// Maps to VSCode's IEditorInput (Section 9.3) combined with file model state.
/// </summary>
public partial class EditorTab : ObservableObject
{
    /// <summary>Full file path.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>Display name (filename).</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>File extension for icon/language detection.</summary>
    public string Extension => Path.GetExtension(FilePath)?.ToLowerInvariant() ?? "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isPreview;

    [ObservableProperty]
    private FileState _fileState = FileState.Saved;

    [ObservableProperty]
    private string _content = "";

    // =========================================================================
    // Per-Tab TextDocument (UI Source of Truth)
    // =========================================================================

    /// <summary>
    /// AvaloniaEdit document instance for this tab.
    /// Switching tabs should swap the editor's Document instead of re-assigning Text,
    /// which is more reliable and avoids event/timing issues.
    /// </summary>
    public TextDocument? Document { get; set; }

    // =========================================================================
    // Version-Based Dirty Detection (Doc Section 5.3)
    // =========================================================================

    /// <summary>
    /// Version ID at last save. Dirty state is derived from
    /// CurrentVersionId != SavedVersionId.
    /// </summary>
    public int SavedVersionId { get; set; }

    /// <summary>
    /// Current version ID, incremented on each content edit.
    /// </summary>
    public int CurrentVersionId { get; set; }

    /// <summary>
    /// Increment version on content change and update dirty state.
    /// </summary>
    public void IncrementVersion()
    {
        CurrentVersionId++;
        var dirty = CurrentVersionId != SavedVersionId;
        if (IsDirty != dirty)
            IsDirty = dirty;
    }

    /// <summary>
    /// Mark as saved at the current version.
    /// </summary>
    public void MarkSaved()
    {
        SavedVersionId = CurrentVersionId;
        IsDirty = false;
    }

    // =========================================================================
    // Disk Metadata for Conflict Detection (Doc Section 5.4)
    // =========================================================================

    /// <summary>SHA256 hash of content when last loaded/saved, for conflict detection.</summary>
    public string? LastKnownDiskHash { get; set; }

    /// <summary>Last known modification time on disk.</summary>
    public DateTime? LastKnownDiskModTime { get; set; }

    /// <summary>
    /// Update disk metadata after loading or saving.
    /// </summary>
    public void UpdateDiskMetadata(string content, DateTime? modifiedTime)
    {
        LastKnownDiskHash = TextFileModel.ComputeHash(content);
        LastKnownDiskModTime = modifiedTime;
    }

    /// <summary>
    /// Check if disk content has changed from what we last knew.
    /// Uses hash comparison when available, falls back to mod time.
    /// </summary>
    public bool HasDiskContentChanged(string diskContent, DateTime diskModTime)
    {
        // Hash comparison is most reliable
        if (LastKnownDiskHash != null)
        {
            var diskHash = TextFileModel.ComputeHash(diskContent);
            return diskHash != LastKnownDiskHash;
        }

        // Fall back to modification time
        if (LastKnownDiskModTime.HasValue)
        {
            return diskModTime > LastKnownDiskModTime.Value;
        }

        // No metadata available, assume changed
        return true;
    }

    // =========================================================================
    // Large File Support
    // =========================================================================

    /// <summary>
    /// For large files: holds the PieceTree buffer to avoid materializing full content.
    /// When set, Content should only hold the viewport portion.
    /// </summary>
    public PieceTreeTextBuffer? TextBuffer { get; set; }

    /// <summary>Whether this tab uses a PieceTree buffer for large file handling.</summary>
    public bool IsLargeFile => TextBuffer != null;

    // =========================================================================
    // Reference to TextFileModel (Doc Section 9.2)
    // =========================================================================

    /// <summary>
    /// Reference to the underlying TextFileModel managed by TextFileService.
    /// Multiple tabs can reference the same model (e.g., in split view).
    /// </summary>
    public TextFileModel? Model { get; set; }

    // =========================================================================
    // Language & Icon Detection
    // =========================================================================

    /// <summary>Language identifier for syntax highlighting.</summary>
    public string Language => Extension switch
    {
        ".cs" => "csharp",
        ".ts" or ".tsx" => "typescript",
        ".js" or ".jsx" => "javascript",
        ".py" => "python",
        ".json" => "json",
        ".xml" or ".axaml" or ".xaml" or ".csproj" or ".props" or ".targets" => "xml",
        ".html" or ".htm" => "html",
        ".css" or ".scss" or ".less" => "css",
        ".md" => "markdown",
        ".yaml" or ".yml" => "yaml",
        ".toml" => "toml",
        ".rs" => "rust",
        ".go" => "go",
        ".java" => "java",
        ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" => "cpp",
        ".c" => "c",
        ".sh" or ".bash" => "shell",
        ".ps1" or ".psm1" => "powershell",
        ".sql" => "sql",
        ".proto" => "protobuf",
        _ => "plaintext"
    };

    /// <summary>Icon glyph based on file type.</summary>
    public string Icon => Extension switch
    {
        ".cs" => "\U0001F7E2",       // green circle for C#
        ".ts" or ".tsx" => "\U0001F535",  // blue circle for TS
        ".js" or ".jsx" => "\U0001F7E1",  // yellow circle for JS
        ".py" => "\U0001F40D",             // snake for Python
        ".json" => "{ }",
        ".xml" or ".axaml" or ".xaml" => "\U0001F4C4",
        ".md" => "\U0001F4DD",
        ".css" or ".scss" => "\U0001F3A8",
        ".html" or ".htm" => "\U0001F310",
        _ => "\U0001F4C4"
    };

    /// <summary>Display title: filename with state indicators.</summary>
    public string DisplayTitle => FileState switch
    {
        FileState.Conflict => $"\u26A0 {FileName}",  // warning sign
        FileState.Orphan => $"\u2620 {FileName}",    // skull
        FileState.Error => $"\u2716 {FileName}",     // X mark
        _ => IsDirty ? $"\u25CF {FileName}" : FileName
    };

    partial void OnIsDirtyChanged(bool value)
    {
        if (value && FileState == FileState.Saved)
            FileState = FileState.Dirty;
        else if (!value)
            FileState = FileState.Saved;
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnFileStateChanged(FileState value) => OnPropertyChanged(nameof(DisplayTitle));

    partial void OnIsPreviewChanged(bool value) => OnPropertyChanged(nameof(DisplayTitle));
}
