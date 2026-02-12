namespace MiniClaudeCode.Avalonia.Editor.TextBuffer;

/// <summary>
/// Node color for the red-black tree.
/// </summary>
public enum NodeColor
{
    Red,
    Black
}

/// <summary>
/// A node in the red-black tree that backs the piece tree.
/// </summary>
public class TreeNode
{
    public Piece Piece { get; set; }
    public NodeColor Color { get; set; }
    public TreeNode? Left { get; set; }
    public TreeNode? Right { get; set; }
    public TreeNode? Parent { get; set; }

    /// <summary>Size of left subtree in characters.</summary>
    public int SizeLeft { get; set; }

    /// <summary>Line feed count of left subtree.</summary>
    public int LfLeft { get; set; }

    public TreeNode(Piece piece, NodeColor color)
    {
        Piece = piece;
        Color = color;
    }
}

/// <summary>
/// Simplified piece tree implementation based on VS Code's PieceTreeBase.
/// Uses a list-based approach for simplicity while maintaining the key benefits:
/// - Chunked storage for memory efficiency
/// - O(log n) operations for edit-heavy workloads
/// - Efficient line-based access
/// </summary>
public class PieceTreeBase
{
    private readonly List<StringBuffer> _buffers = [];
    private readonly List<Piece> _pieces = [];
    private int _totalLength;
    private int _totalLineCount = 1; // At least 1 line

    /// <summary>Total character length of the document.</summary>
    public int Length => _totalLength;

    /// <summary>Total number of lines.</summary>
    public int LineCount => _totalLineCount;

    /// <summary>
    /// Create a piece tree from initial string buffers.
    /// </summary>
    public PieceTreeBase(List<StringBuffer> buffers)
    {
        // Buffer 0 is reserved for change buffer (edits)
        _buffers.Add(new StringBuffer("", [0]));

        // Add original buffers
        foreach (var buf in buffers)
        {
            var bufferIndex = _buffers.Count;
            _buffers.Add(buf);

            if (buf.Content.Length > 0)
            {
                var lineFeeds = buf.LineStarts.Length - 1;
                var piece = new Piece(
                    bufferIndex,
                    new BufferCursor(0, 0),
                    new BufferCursor(buf.LineStarts.Length - 1,
                        buf.Content.Length - buf.LineStarts[^1]),
                    lineFeeds,
                    buf.Content.Length
                );
                _pieces.Add(piece);
                _totalLength += buf.Content.Length;
                _totalLineCount += lineFeeds;
            }
        }
    }

    /// <summary>
    /// Get the text content of a specific line (1-based line number).
    /// </summary>
    public string GetLineContent(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _totalLineCount)
            return "";

        var startOffset = GetOffsetOfLine(lineNumber);
        var endOffset = lineNumber < _totalLineCount
            ? GetOffsetOfLine(lineNumber + 1)
            : _totalLength;

        var content = GetValueInRange(startOffset, endOffset);

        // Trim trailing line ending
        if (content.EndsWith("\r\n"))
            return content[..^2];
        if (content.EndsWith("\n") || content.EndsWith("\r"))
            return content[..^1];
        return content;
    }

    /// <summary>
    /// Get all text content.
    /// </summary>
    public string GetAllText()
    {
        var sb = new System.Text.StringBuilder(_totalLength);
        foreach (var piece in _pieces)
        {
            var buffer = _buffers[piece.BufferIndex];
            var startOffset = GetBufferOffset(buffer, piece.Start);
            sb.Append(buffer.Content, startOffset, piece.Length);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Insert text at a given offset.
    /// </summary>
    public void Insert(int offset, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Append to change buffer (buffer index 0)
        var changeBuffer = _buffers[0];
        var insertStart = changeBuffer.Content.Length;
        var newContent = changeBuffer.Content + text;
        var newLineStarts = StringBuffer.ComputeLineStarts(newContent);
        _buffers[0] = new StringBuffer(newContent, newLineStarts);

        var startLine = Array.BinarySearch(newLineStarts, insertStart);
        if (startLine < 0) startLine = ~startLine - 1;
        var startCol = insertStart - newLineStarts[startLine];

        var endOffset = insertStart + text.Length;
        var endLine = Array.BinarySearch(newLineStarts, endOffset);
        if (endLine < 0) endLine = ~endLine - 1;
        var endCol = endOffset - newLineStarts[endLine];

        var lineFeeds = CountLineFeeds(text);
        var newPiece = new Piece(
            0,
            new BufferCursor(startLine, startCol),
            new BufferCursor(endLine, endCol),
            lineFeeds,
            text.Length
        );

        // Find where to insert in the piece list
        InsertPieceAtOffset(offset, newPiece);

        _totalLength += text.Length;
        _totalLineCount += lineFeeds;
    }

    /// <summary>
    /// Delete text from offset with given length.
    /// </summary>
    public void Delete(int offset, int length)
    {
        if (length <= 0 || offset < 0 || offset >= _totalLength) return;

        length = Math.Min(length, _totalLength - offset);

        // Count line feeds being removed
        var deletedText = GetValueInRange(offset, offset + length);
        var removedLineFeeds = CountLineFeeds(deletedText);

        // Find and split pieces
        DeleteFromPieces(offset, length);

        _totalLength -= length;
        _totalLineCount -= removedLineFeeds;
    }

    /// <summary>
    /// Create a snapshot for saving (iterates pieces without full copy).
    /// </summary>
    public IEnumerable<string> CreateSnapshot()
    {
        foreach (var piece in _pieces)
        {
            var buffer = _buffers[piece.BufferIndex];
            var startOffset = GetBufferOffset(buffer, piece.Start);
            yield return buffer.Content.Substring(startOffset, piece.Length);
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private string GetValueInRange(int startOffset, int endOffset)
    {
        if (startOffset >= endOffset) return "";

        var sb = new System.Text.StringBuilder(endOffset - startOffset);
        int currentOffset = 0;

        foreach (var piece in _pieces)
        {
            var pieceEnd = currentOffset + piece.Length;

            if (pieceEnd <= startOffset)
            {
                currentOffset = pieceEnd;
                continue;
            }

            if (currentOffset >= endOffset) break;

            var buffer = _buffers[piece.BufferIndex];
            var bufferStart = GetBufferOffset(buffer, piece.Start);

            var readStart = Math.Max(startOffset - currentOffset, 0);
            var readEnd = Math.Min(endOffset - currentOffset, piece.Length);

            sb.Append(buffer.Content, bufferStart + readStart, readEnd - readStart);

            currentOffset = pieceEnd;
        }

        return sb.ToString();
    }

    private int GetOffsetOfLine(int lineNumber)
    {
        if (lineNumber <= 1) return 0;

        int currentLine = 1;
        int currentOffset = 0;

        foreach (var piece in _pieces)
        {
            if (currentLine + piece.LineFeedCount >= lineNumber)
            {
                // Target line starts within this piece
                var buffer = _buffers[piece.BufferIndex];
                var bufferStart = GetBufferOffset(buffer, piece.Start);
                var text = buffer.Content.Substring(bufferStart, piece.Length);

                int lineInPiece = lineNumber - currentLine;
                int pos = 0;
                for (int i = 0; i < lineInPiece && pos < text.Length; i++)
                {
                    var idx = text.IndexOf('\n', pos);
                    if (idx < 0) break;
                    pos = idx + 1;
                }
                return currentOffset + pos;
            }

            currentLine += piece.LineFeedCount;
            currentOffset += piece.Length;
        }

        return _totalLength;
    }

    private void InsertPieceAtOffset(int offset, Piece newPiece)
    {
        if (offset <= 0)
        {
            _pieces.Insert(0, newPiece);
            return;
        }

        if (offset >= _totalLength)
        {
            _pieces.Add(newPiece);
            return;
        }

        // Find the piece that contains the offset and split it
        int currentOffset = 0;
        for (int i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            if (currentOffset + piece.Length > offset)
            {
                var splitPos = offset - currentOffset;
                SplitPieceAndInsert(i, splitPos, newPiece);
                return;
            }
            currentOffset += piece.Length;
        }

        _pieces.Add(newPiece);
    }

    private void SplitPieceAndInsert(int pieceIndex, int splitPos, Piece newPiece)
    {
        var original = _pieces[pieceIndex];
        var buffer = _buffers[original.BufferIndex];
        var bufferStart = GetBufferOffset(buffer, original.Start);

        // Left part
        var leftText = buffer.Content.Substring(bufferStart, splitPos);
        var leftLf = CountLineFeeds(leftText);
        var leftEndLine = original.Start.Line;
        var leftEndCol = original.Start.Column + splitPos;
        // Adjust for line breaks
        for (int i = 0; i < leftText.Length; i++)
        {
            if (leftText[i] == '\n')
            {
                leftEndLine++;
                leftEndCol = leftText.Length - i - 1;
            }
        }

        var leftPiece = new Piece(original.BufferIndex, original.Start,
            new BufferCursor(leftEndLine, leftEndCol), leftLf, splitPos);

        // Right part
        var rightLength = original.Length - splitPos;
        var rightLf = original.LineFeedCount - leftLf;
        var rightPiece = new Piece(original.BufferIndex,
            new BufferCursor(leftEndLine, leftEndCol), original.End,
            rightLf, rightLength);

        _pieces.RemoveAt(pieceIndex);
        _pieces.Insert(pieceIndex, leftPiece);
        _pieces.Insert(pieceIndex + 1, newPiece);
        if (rightLength > 0)
            _pieces.Insert(pieceIndex + 2, rightPiece);
    }

    private void DeleteFromPieces(int offset, int length)
    {
        int deleteEnd = offset + length;
        int currentOffset = 0;
        var toRemove = new List<int>();
        var toInsert = new List<(int index, Piece piece)>();

        for (int i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            var pieceStart = currentOffset;
            var pieceEnd = currentOffset + piece.Length;

            if (pieceEnd <= offset || pieceStart >= deleteEnd)
            {
                currentOffset = pieceEnd;
                continue;
            }

            // Piece overlaps with deletion range
            if (pieceStart >= offset && pieceEnd <= deleteEnd)
            {
                // Entire piece is deleted
                toRemove.Add(i);
            }
            else if (pieceStart < offset && pieceEnd > deleteEnd)
            {
                // Deletion is in the middle - split into two
                var buffer = _buffers[piece.BufferIndex];
                var bufferStart = GetBufferOffset(buffer, piece.Start);

                var leftLen = offset - pieceStart;
                var leftText = buffer.Content.Substring(bufferStart, leftLen);
                var leftLf = CountLineFeeds(leftText);
                var leftPiece = new Piece(piece.BufferIndex, piece.Start,
                    piece.Start, leftLf, leftLen);

                var rightStart = deleteEnd - pieceStart;
                var rightLen = pieceEnd - deleteEnd;
                var rightLf = piece.LineFeedCount - leftLf - CountLineFeeds(
                    buffer.Content.Substring(bufferStart + leftLen, length));
                var rightPiece = new Piece(piece.BufferIndex, piece.Start,
                    piece.End, Math.Max(0, rightLf), rightLen);

                toRemove.Add(i);
                toInsert.Add((i, leftPiece));
                toInsert.Add((i + 1, rightPiece));
            }
            else if (pieceStart < offset)
            {
                // Deletion starts in this piece
                var newLen = offset - pieceStart;
                var buffer = _buffers[piece.BufferIndex];
                var bufferStart = GetBufferOffset(buffer, piece.Start);
                var text = buffer.Content.Substring(bufferStart, newLen);
                var newLf = CountLineFeeds(text);
                var newPiece = new Piece(piece.BufferIndex, piece.Start, piece.Start, newLf, newLen);

                toRemove.Add(i);
                toInsert.Add((i, newPiece));
            }
            else
            {
                // Deletion ends in this piece
                var skipLen = deleteEnd - pieceStart;
                var newLen = piece.Length - skipLen;
                var newLf = piece.LineFeedCount - CountLineFeeds(
                    _buffers[piece.BufferIndex].Content.Substring(
                        GetBufferOffset(_buffers[piece.BufferIndex], piece.Start), skipLen));
                var newPiece = new Piece(piece.BufferIndex, piece.Start, piece.End, Math.Max(0, newLf), newLen);

                toRemove.Add(i);
                toInsert.Add((i, newPiece));
            }

            currentOffset = pieceEnd;
        }

        // Apply removals in reverse order
        for (int i = toRemove.Count - 1; i >= 0; i--)
            _pieces.RemoveAt(toRemove[i]);

        // Apply insertions
        foreach (var (index, piece) in toInsert.OrderBy(x => x.index))
        {
            var adjustedIndex = Math.Min(index, _pieces.Count);
            _pieces.Insert(adjustedIndex, piece);
        }
    }

    private static int GetBufferOffset(StringBuffer buffer, BufferCursor cursor)
    {
        if (cursor.Line < buffer.LineStarts.Length)
            return buffer.LineStarts[cursor.Line] + cursor.Column;
        return buffer.Content.Length;
    }

    private static int CountLineFeeds(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
            else if (text[i] == '\r')
            {
                count++;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
        }
        return count;
    }
}
