namespace MiniClaudeCode.Avalonia.Editor;

/// <summary>
/// Constants matching VS Code's large file thresholds from textModel.ts.
/// </summary>
public static class LargeFileConstants
{
    /// <summary>Files larger than 20 MB disable tokenization.</summary>
    public const long LargeFileSizeThreshold = 20 * 1024 * 1024;

    /// <summary>Files with more than 300K lines disable tokenization.</summary>
    public const int LargeFileLineCountThreshold = 300_000;

    /// <summary>Files larger than 256M characters disable heap-intensive operations.</summary>
    public const long HeapOperationThreshold = 256L * 1024 * 1024;

    /// <summary>Files larger than 50 MB disable model syncing to extensions.</summary>
    public const long ModelSyncLimit = 50 * 1024 * 1024;

    /// <summary>Average chunk size for piece tree (~64KB).</summary>
    public const int AverageChunkSize = 65535;

    /// <summary>Lines longer than this are not tokenized.</summary>
    public const int MaxTokenizationLineLength = 20_000;

    /// <summary>Maximum file size the diff editor will handle.</summary>
    public const long DiffEditorMaxFileSize = 50 * 1024 * 1024;
}
