using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Event args for TextFileModel dirty state changes.
/// </summary>
public record TextFileModelChangeEvent(string Resource, bool IsDirty);

/// <summary>
/// Event args for TextFileModel save events.
/// </summary>
public record TextFileSaveEvent(string Resource);

/// <summary>
/// Event args for TextFileModel load events.
/// </summary>
public record TextFileLoadEvent(string Resource, string Content);

/// <summary>
/// Save options for TextFileService.
/// </summary>
public class SaveOptions
{
    /// <summary>Whether to create a backup before saving.</summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>Save reason for event hooks.</summary>
    public SaveReason Reason { get; set; } = SaveReason.Explicit;
}

/// <summary>
/// Reason for saving a file.
/// </summary>
public enum SaveReason
{
    Explicit,
    AutoSave,
    WindowClose,
    FocusChange
}

/// <summary>
/// In-memory representation of a text file, cached by TextFileService.
/// Maps to VSCode's ITextFileEditorModel (Section 9.2).
/// 
/// Tracks content, dirty state via version IDs, disk metadata for conflict
/// detection, and reference counting for disposal.
/// </summary>
public partial class TextFileModel : ObservableObject
{
    /// <summary>Full file path (resource URI equivalent).</summary>
    public string Resource { get; }

    /// <summary>Current in-memory content.</summary>
    [ObservableProperty]
    private string _content = "";

    /// <summary>Text encoding.</summary>
    public string Encoding { get; set; } = "utf-8";

    /// <summary>Whether the model has been resolved (loaded from disk).</summary>
    [ObservableProperty]
    private bool _isResolved;

    /// <summary>Whether the file has been deleted on disk while open (orphaned).</summary>
    [ObservableProperty]
    private bool _isOrphaned;

    /// <summary>Version ID at last save (doc 5.3).</summary>
    public int SavedVersionId { get; set; }

    /// <summary>Current version ID, incremented on each edit (doc 5.3).</summary>
    public int CurrentVersionId { get; set; }

    /// <summary>Whether content differs from last saved version.</summary>
    public bool IsDirty => CurrentVersionId != SavedVersionId;

    /// <summary>SHA256 hash of content when last loaded/saved, for conflict detection (doc 5.4).</summary>
    public string? DiskHash { get; set; }

    /// <summary>Last known modification time on disk.</summary>
    public DateTime? DiskModifiedTime { get; set; }

    /// <summary>For large files: PieceTree buffer to avoid materializing full content.</summary>
    public PieceTreeTextBuffer? TextBuffer { get; set; }

    /// <summary>Whether this model uses a PieceTree buffer for large file handling.</summary>
    public bool IsLargeFile => TextBuffer != null;

    /// <summary>Number of tabs/editors referencing this model (doc 8.1 step 4).</summary>
    public int ReferenceCount { get; set; }

    /// <summary>Fired when content changes.</summary>
    public event Action<TextFileModel>? OnDidChangeContent;

    /// <summary>Fired when dirty state changes.</summary>
    public event Action<TextFileModelChangeEvent>? OnDidChangeDirty;

    /// <summary>Fired after a successful save.</summary>
    public event Action<TextFileSaveEvent>? OnDidSave;

    /// <summary>Fired before the model is disposed.</summary>
    public event Action<TextFileModel>? OnWillDispose;

    public TextFileModel(string resource)
    {
        Resource = resource;
    }

    /// <summary>
    /// Apply an edit to the content, incrementing the version ID.
    /// Fires OnDidChangeContent and OnDidChangeDirty events.
    /// </summary>
    public void ApplyEdit(string newContent)
    {
        var wasDirty = IsDirty;
        Content = newContent;
        CurrentVersionId++;

        OnDidChangeContent?.Invoke(this);

        if (wasDirty != IsDirty)
        {
            OnDidChangeDirty?.Invoke(new TextFileModelChangeEvent(Resource, IsDirty));
        }
    }

    /// <summary>
    /// Mark the model as saved at the current version.
    /// </summary>
    public void MarkSaved()
    {
        var wasDirty = IsDirty;
        SavedVersionId = CurrentVersionId;

        if (wasDirty)
        {
            OnDidChangeDirty?.Invoke(new TextFileModelChangeEvent(Resource, false));
        }

        OnDidSave?.Invoke(new TextFileSaveEvent(Resource));
    }

    /// <summary>
    /// Revert to a specific content (e.g., disk content).
    /// Resets version IDs.
    /// </summary>
    public void Revert(string diskContent)
    {
        var wasDirty = IsDirty;
        Content = diskContent;
        CurrentVersionId++;
        SavedVersionId = CurrentVersionId;

        OnDidChangeContent?.Invoke(this);

        if (wasDirty)
        {
            OnDidChangeDirty?.Invoke(new TextFileModelChangeEvent(Resource, false));
        }
    }

    /// <summary>
    /// Compute SHA256 hash of a string for conflict detection.
    /// </summary>
    public static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Update disk metadata after loading or saving.
    /// </summary>
    public void UpdateDiskMetadata(string content, DateTime? modifiedTime)
    {
        DiskHash = ComputeHash(content);
        DiskModifiedTime = modifiedTime;
    }

    /// <summary>
    /// Dispose the model and fire OnWillDispose.
    /// </summary>
    public void Dispose()
    {
        OnWillDispose?.Invoke(this);
        TextBuffer = null;
    }
}
