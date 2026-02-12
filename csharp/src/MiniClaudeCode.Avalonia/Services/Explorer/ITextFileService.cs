using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services.Explorer;

/// <summary>
/// Service interface for text file model management with caching,
/// dirty tracking, and save semantics.
/// Maps to VSCode's ITextFileService (Section 3.3).
/// </summary>
public interface ITextFileService
{
    /// <summary>Fired when a model's dirty state changes.</summary>
    event Action<TextFileModelChangeEvent>? OnDidChangeDirty;

    /// <summary>Fired after a successful save.</summary>
    event Action<TextFileSaveEvent>? OnDidSave;

    /// <summary>Fired after a model is loaded from disk.</summary>
    event Action<TextFileLoadEvent>? OnDidLoad;

    /// <summary>Fired before a save is attempted (extensions can modify content).</summary>
    event Func<string, Task>? OnWillSave;

    /// <summary>
    /// Get a cached model by file path. Returns null if not loaded.
    /// </summary>
    TextFileModel? GetModel(string filePath);

    /// <summary>
    /// Resolve (load) a text file model. Returns from cache if available,
    /// otherwise reads from disk. Increments reference count.
    /// </summary>
    Task<TextFileModel> ResolveAsync(string filePath);

    /// <summary>
    /// Save a model to disk with backup support.
    /// </summary>
    Task<bool> SaveAsync(string filePath, SaveOptions? options = null);

    /// <summary>
    /// Revert a model to its on-disk content.
    /// </summary>
    Task<bool> RevertAsync(string filePath);

    /// <summary>
    /// Release a reference to a model. When reference count reaches 0,
    /// the model is disposed and removed from cache.
    /// </summary>
    void ReleaseModel(string filePath);

    /// <summary>
    /// Get all file paths that have dirty (unsaved) models.
    /// </summary>
    IReadOnlyList<string> GetDirtyFiles();

    /// <summary>
    /// Handle an external file change event for an already-loaded model.
    /// Returns the action taken.
    /// </summary>
    Task<ExternalChangeResult> HandleExternalChangeAsync(string filePath);

    /// <summary>
    /// Handle an external file deletion for an already-loaded model.
    /// </summary>
    void HandleExternalDeletion(string filePath);
}

/// <summary>
/// Result of handling an external file change.
/// </summary>
public enum ExternalChangeResult
{
    /// <summary>No change detected or file not loaded.</summary>
    NoChange,

    /// <summary>File was clean; automatically reloaded.</summary>
    AutoReloaded,

    /// <summary>File was dirty; conflict detected.</summary>
    Conflict,

    /// <summary>Error reading the changed file.</summary>
    Error
}
