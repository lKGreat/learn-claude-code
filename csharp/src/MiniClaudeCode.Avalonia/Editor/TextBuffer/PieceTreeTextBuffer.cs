namespace MiniClaudeCode.Avalonia.Editor.TextBuffer;

/// <summary>
/// High-level text buffer built on top of PieceTreeBase.
/// Handles file loading in chunks and provides document-level operations.
/// Based on VS Code's PieceTreeTextBuffer.
/// </summary>
public class PieceTreeTextBuffer
{
    private readonly PieceTreeBase _pieceTree;

    /// <summary>Whether this file was detected as a large file.</summary>
    public bool IsLargeFile { get; }

    /// <summary>Whether tokenization should be disabled for this file.</summary>
    public bool IsTooLargeForTokenization { get; }

    /// <summary>Whether syncing to extensions should be disabled.</summary>
    public bool IsTooLargeForSyncing { get; }

    /// <summary>Whether heap-intensive operations should be disabled.</summary>
    public bool IsTooLargeForHeapOperation { get; }

    /// <summary>Total character length.</summary>
    public int Length => _pieceTree.Length;

    /// <summary>Total line count.</summary>
    public int LineCount => _pieceTree.LineCount;

    public PieceTreeTextBuffer(PieceTreeBase pieceTree, long fileSize, bool largeFileOptimizations = true)
    {
        _pieceTree = pieceTree;

        if (largeFileOptimizations)
        {
            IsTooLargeForTokenization =
                fileSize > LargeFileConstants.LargeFileSizeThreshold ||
                pieceTree.LineCount > LargeFileConstants.LargeFileLineCountThreshold;

            IsTooLargeForSyncing = fileSize > LargeFileConstants.ModelSyncLimit;

            IsTooLargeForHeapOperation = fileSize > LargeFileConstants.HeapOperationThreshold;

            IsLargeFile = IsTooLargeForTokenization;
        }
    }

    /// <summary>Get the content of a specific line (1-based).</summary>
    public string GetLineContent(int lineNumber) => _pieceTree.GetLineContent(lineNumber);

    /// <summary>Get all text content.</summary>
    public string GetAllText() => _pieceTree.GetAllText();

    /// <summary>Insert text at offset.</summary>
    public void Insert(int offset, string text) => _pieceTree.Insert(offset, text);

    /// <summary>Delete text at offset with length.</summary>
    public void Delete(int offset, int length) => _pieceTree.Delete(offset, length);

    /// <summary>Create a snapshot for efficient saving.</summary>
    public IEnumerable<string> CreateSnapshot() => _pieceTree.CreateSnapshot();
}

/// <summary>
/// Builder that accepts file content in chunks and constructs a PieceTreeTextBuffer.
/// Based on VS Code's PieceTreeTextBufferBuilder.
/// </summary>
public class PieceTreeTextBufferBuilder
{
    private readonly List<StringBuffer> _chunks = [];
    private long _totalSize;
    private bool _containsBom;

    /// <summary>
    /// Accept a chunk of text from the file reader.
    /// </summary>
    public void AcceptChunk(string chunk)
    {
        if (_totalSize == 0 && chunk.Length > 0 && chunk[0] == '\uFEFF')
        {
            // Strip BOM
            _containsBom = true;
            chunk = chunk[1..];
        }

        if (chunk.Length == 0) return;

        var lineStarts = StringBuffer.ComputeLineStarts(chunk);
        _chunks.Add(new StringBuffer(chunk, lineStarts));
        _totalSize += chunk.Length;
    }

    /// <summary>
    /// Build the PieceTreeTextBuffer from accumulated chunks.
    /// </summary>
    public PieceTreeTextBuffer Build(bool largeFileOptimizations = true)
    {
        var pieceTree = new PieceTreeBase(_chunks);
        return new PieceTreeTextBuffer(pieceTree, _totalSize, largeFileOptimizations);
    }

    /// <summary>
    /// Load a file asynchronously in chunks and return a PieceTreeTextBuffer.
    /// </summary>
    public static async Task<PieceTreeTextBuffer> LoadFileAsync(string path, bool largeFileOptimizations = true)
    {
        var builder = new PieceTreeTextBufferBuilder();
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        var buffer = new char[LargeFileConstants.AverageChunkSize];
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            builder.AcceptChunk(new string(buffer, 0, read));
        }
        return builder.Build(largeFileOptimizations);
    }
}
