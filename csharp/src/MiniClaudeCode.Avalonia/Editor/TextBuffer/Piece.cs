namespace MiniClaudeCode.Avalonia.Editor.TextBuffer;

/// <summary>
/// A piece in the piece tree. Points to a range within a StringBuffer.
/// Based on VS Code's Piece in pieceTreeBase.ts.
/// </summary>
public class Piece
{
    /// <summary>Index of the buffer this piece refers to.</summary>
    public int BufferIndex { get; }

    /// <summary>Start position within the buffer (line, column).</summary>
    public BufferCursor Start { get; }

    /// <summary>End position within the buffer (line, column).</summary>
    public BufferCursor End { get; }

    /// <summary>Number of line feeds in this piece.</summary>
    public int LineFeedCount { get; }

    /// <summary>Total character length of this piece.</summary>
    public int Length { get; }

    public Piece(int bufferIndex, BufferCursor start, BufferCursor end, int lineFeedCount, int length)
    {
        BufferIndex = bufferIndex;
        Start = start;
        End = end;
        LineFeedCount = lineFeedCount;
        Length = length;
    }
}

/// <summary>
/// A cursor position within a StringBuffer, represented as (line, column).
/// </summary>
public readonly struct BufferCursor
{
    /// <summary>0-based line index within the buffer.</summary>
    public int Line { get; }

    /// <summary>0-based column offset within that line.</summary>
    public int Column { get; }

    public BufferCursor(int line, int column)
    {
        Line = line;
        Column = column;
    }
}
