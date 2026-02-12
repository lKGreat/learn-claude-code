namespace MiniClaudeCode.Abstractions.Indexing;

/// <summary>
/// Manages file-level indexing of the codebase for fast file search and navigation.
/// Supports incremental updates and fuzzy search.
/// </summary>
public interface ICodebaseIndex
{
    /// <summary>
    /// Performs a full index build of the workspace starting from the root path.
    /// This operation may take significant time for large codebases.
    /// </summary>
    /// <param name="rootPath">Absolute path to the workspace root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when indexing is done.</returns>
    Task IndexWorkspaceAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for files matching the query using fuzzy matching.
    /// Returns results ordered by relevance.
    /// </summary>
    /// <param name="query">The search query (file name or partial path).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching file index entries.</returns>
    Task<IReadOnlyList<FileIndexEntry>> SearchFilesAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata for a specific file from the index.
    /// Returns null if the file is not in the index.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File metadata, or null if not found.</returns>
    Task<FileIndexEntry?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the index that a file has been created, modified, or deleted.
    /// Triggers an incremental index update for the affected file.
    /// </summary>
    /// <param name="filePath">Absolute path to the changed file.</param>
    /// <param name="changeType">Type of change (created, modified, deleted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the incremental update is done.</returns>
    Task OnFileChangedAsync(
        string filePath,
        FileChangeType changeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the workspace has been indexed at least once.
    /// </summary>
    bool IsIndexed { get; }

    /// <summary>
    /// Gets the total number of files currently in the index.
    /// </summary>
    int FileCount { get; }
}

/// <summary>
/// Metadata entry for a file in the codebase index.
/// </summary>
public record FileIndexEntry
{
    /// <summary>Absolute path to the file.</summary>
    public required string Path { get; init; }

    /// <summary>Language identifier (e.g., "csharp", "typescript", "python").</summary>
    public required string Language { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Last modified timestamp.</summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>List of top-level symbols defined in this file.</summary>
    public IReadOnlyList<string>? Symbols { get; init; }

    /// <summary>Relevance score for search results (0.0 to 1.0).</summary>
    public double RelevanceScore { get; init; }
}

/// <summary>
/// Type of file system change.
/// </summary>
public enum FileChangeType
{
    /// <summary>File was created.</summary>
    Created,

    /// <summary>File was modified.</summary>
    Modified,

    /// <summary>File was deleted.</summary>
    Deleted
}
