using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace MiniClaudeCode.Avalonia.Editor.Folding;

/// <summary>
/// Folding strategy based on indentation levels.
/// Creates fold regions when indentation increases and ends when it returns to the original level.
/// Also supports brace-based folding for C-style languages.
/// </summary>
public sealed class IndentFoldingStrategy
{
    private int _tabSize = 4;

    public int TabSize
    {
        get => _tabSize;
        set => _tabSize = Math.Max(1, value);
    }

    /// <summary>
    /// Updates the folding manager with new fold regions based on the document's indentation.
    /// </summary>
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var newFoldings = CreateNewFoldings(document);
        manager.UpdateFoldings(newFoldings, -1);
    }

    private List<NewFolding> CreateNewFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();

        // Brace-based folding
        AddBraceFoldings(document, foldings);

        // Indent-based folding (for languages without braces)
        AddIndentFoldings(document, foldings);

        // Sort by start offset
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        // Remove duplicates/overlapping regions that are too similar
        return DeduplicateFoldings(foldings);
    }

    private void AddBraceFoldings(TextDocument document, List<NewFolding> foldings)
    {
        var openBraces = new Stack<int>();
        var text = document.Text;

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '{')
            {
                openBraces.Push(i);
            }
            else if (ch == '}' && openBraces.Count > 0)
            {
                var startOffset = openBraces.Pop();
                if (i - startOffset > 1) // Don't fold empty braces
                {
                    // Check if they span multiple lines
                    var startLine = document.GetLineByOffset(startOffset);
                    var endLine = document.GetLineByOffset(i);
                    if (endLine.LineNumber > startLine.LineNumber)
                    {
                        var name = GetFoldingName(document, startLine);
                        foldings.Add(new NewFolding(startOffset, i + 1) { Name = name });
                    }
                }
            }
        }
    }

    private void AddIndentFoldings(TextDocument document, List<NewFolding> foldings)
    {
        if (document.LineCount < 3) return;

        var lineIndents = new int[document.LineCount + 1];
        var lineEmpty = new bool[document.LineCount + 1];

        // Calculate indent levels for all lines
        for (int i = 1; i <= document.LineCount; i++)
        {
            var line = document.GetLineByNumber(i);
            var text = document.GetText(line.Offset, line.Length);
            lineEmpty[i] = string.IsNullOrWhiteSpace(text);
            lineIndents[i] = lineEmpty[i] ? -1 : GetIndentLevel(text);
        }

        // Find fold regions: a fold starts when indent increases and ends when it returns
        for (int i = 1; i < document.LineCount; i++)
        {
            if (lineEmpty[i]) continue;

            var currentIndent = lineIndents[i];

            // Find the next non-empty line
            int nextNonEmpty = i + 1;
            while (nextNonEmpty <= document.LineCount && lineEmpty[nextNonEmpty])
                nextNonEmpty++;

            if (nextNonEmpty > document.LineCount) break;

            // If next line is more indented, start a fold region
            if (lineIndents[nextNonEmpty] > currentIndent)
            {
                // Find end of this indent block
                int endLine = nextNonEmpty;
                for (int j = nextNonEmpty + 1; j <= document.LineCount; j++)
                {
                    if (lineEmpty[j]) continue;
                    if (lineIndents[j] <= currentIndent) break;
                    endLine = j;
                }

                if (endLine > i + 1)
                {
                    var startLine = document.GetLineByNumber(i);
                    var lastLine = document.GetLineByNumber(endLine);
                    var name = document.GetText(startLine.Offset, startLine.Length).Trim();
                    if (name.Length > 40) name = name[..40] + "...";

                    foldings.Add(new NewFolding(startLine.EndOffset, lastLine.EndOffset)
                    {
                        Name = name
                    });
                }
            }
        }
    }

    private int GetIndentLevel(string lineText)
    {
        int spaces = 0;
        foreach (var ch in lineText)
        {
            if (ch == ' ') spaces++;
            else if (ch == '\t') spaces += _tabSize;
            else break;
        }
        return spaces / _tabSize;
    }

    private static string GetFoldingName(TextDocument document, DocumentLine startLine)
    {
        var text = document.GetText(startLine.Offset, startLine.Length).Trim();
        // Remove the opening brace for the display name
        if (text.EndsWith('{'))
            text = text[..^1].TrimEnd();
        if (text.Length > 50)
            text = text[..50] + "...";
        return text.Length > 0 ? text : "{...}";
    }

    private static List<NewFolding> DeduplicateFoldings(List<NewFolding> foldings)
    {
        if (foldings.Count <= 1) return foldings;

        var result = new List<NewFolding> { foldings[0] };
        for (int i = 1; i < foldings.Count; i++)
        {
            var prev = result[^1];
            var curr = foldings[i];

            // Skip if too similar (same start, similar end)
            if (Math.Abs(curr.StartOffset - prev.StartOffset) < 3 &&
                Math.Abs(curr.EndOffset - prev.EndOffset) < 3)
                continue;

            result.Add(curr);
        }
        return result;
    }
}
