namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// File system stat information for a file or directory.
/// Maps to VSCode's IFileStat (Section 9.5).
/// </summary>
public record FileStat
{
    /// <summary>Full file path (resource URI equivalent).</summary>
    public required string Resource { get; init; }

    /// <summary>File or directory name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a regular file.</summary>
    public bool IsFile { get; init; }

    /// <summary>Whether this is a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Whether this is a symbolic link.</summary>
    public bool IsSymbolicLink { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTime ModifiedTime { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedTime { get; init; }

    /// <summary>Child entries (for directories).</summary>
    public FileStat[]? Children { get; init; }

    /// <summary>
    /// Create a FileStat from a FileInfo.
    /// </summary>
    public static FileStat FromFileInfo(FileInfo fi) => new()
    {
        Resource = fi.FullName,
        Name = fi.Name,
        IsFile = true,
        IsDirectory = false,
        IsSymbolicLink = fi.LinkTarget != null,
        Size = fi.Exists ? fi.Length : 0,
        ModifiedTime = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue,
        CreatedTime = fi.Exists ? fi.CreationTimeUtc : DateTime.MinValue
    };

    /// <summary>
    /// Create a FileStat from a DirectoryInfo.
    /// </summary>
    public static FileStat FromDirectoryInfo(DirectoryInfo di) => new()
    {
        Resource = di.FullName,
        Name = di.Name,
        IsFile = false,
        IsDirectory = true,
        IsSymbolicLink = di.LinkTarget != null,
        Size = 0,
        ModifiedTime = di.Exists ? di.LastWriteTimeUtc : DateTime.MinValue,
        CreatedTime = di.Exists ? di.CreationTimeUtc : DateTime.MinValue
    };
}
