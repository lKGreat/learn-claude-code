namespace MiniClaudeCode.Avalonia.Editor.TextBuffer;

/// <summary>
/// An immutable string buffer chunk used by the piece tree.
/// Stores original file content in ~64KB chunks.
/// Based on VS Code's StringBuffer in pieceTreeBase.ts.
/// </summary>
public class StringBuffer
{
    /// <summary>The actual text content.</summary>
    public string Content { get; }

    /// <summary>Cached line start offsets within this buffer.</summary>
    public int[] LineStarts { get; }

    public StringBuffer(string content, int[] lineStarts)
    {
        Content = content;
        LineStarts = lineStarts;
    }

    /// <summary>
    /// Compute line start offsets for a string.
    /// Returns array of offsets where each line begins.
    /// </summary>
    public static int[] ComputeLineStarts(string content)
    {
        var result = new List<int> { 0 };
        for (int i = 0; i < content.Length; i++)
        {
            char ch = content[i];
            if (ch == '\r')
            {
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++; // Skip \n in \r\n
                }
                result.Add(i + 1);
            }
            else if (ch == '\n')
            {
                result.Add(i + 1);
            }
        }
        return result.ToArray();
    }
}
