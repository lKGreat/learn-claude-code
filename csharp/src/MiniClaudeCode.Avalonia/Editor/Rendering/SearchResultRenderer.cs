using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace MiniClaudeCode.Avalonia.Editor.Rendering;

/// <summary>
/// Highlights all search matches in the editor background.
/// </summary>
public class SearchResultRenderer : IBackgroundRenderer
{
    private readonly List<(int Offset, int Length)> _matches = [];
    private int _currentMatchIndex = -1;

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>
    /// Update the set of matches to highlight.
    /// </summary>
    public void SetMatches(IEnumerable<(int Offset, int Length)> matches, int currentIndex = -1)
    {
        _matches.Clear();
        _matches.AddRange(matches);
        _currentMatchIndex = currentIndex;
    }

    /// <summary>
    /// Clear all highlights.
    /// </summary>
    public void Clear()
    {
        _matches.Clear();
        _currentMatchIndex = -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0) return;
        if (textView.Document == null) return;

        // 切换 Document 的瞬间，旧 offset/segment 可能越界，导致 AvaloniaEdit 内部抛 ArgumentException（first-chance）。
        // 这里做硬校验 + 保护性 try/catch，避免异常被抛出（从源头消除 VS 输出的 first-chance）。
        var docLen = textView.Document.TextLength;
        if (docLen <= 0) return;

        var matchBrush = new SolidColorBrush(Color.Parse("#3389B4FA")); // semi-transparent blue
        var currentBrush = new SolidColorBrush(Color.Parse("#6689B4FA")); // brighter for current

        try
        {
            for (int i = 0; i < _matches.Count; i++)
            {
                var (offset, length) = _matches[i];
                if (length <= 0) continue;
                if (offset < 0) continue;
                if (offset >= docLen) continue;
                if (offset + length > docLen) continue;

                var brush = i == _currentMatchIndex ? currentBrush : matchBrush;

                var segment = new TextSegment { StartOffset = offset, Length = length };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                    drawingContext.FillRectangle(brush, rect);
            }
        }
        catch
        {
            // ignore: best-effort rendering
        }
    }

    /// <summary>
    /// Perform a search on a document and return match offsets.
    /// </summary>
    public static List<(int Offset, int Length)> FindAll(
        TextDocument document,
        string searchText,
        bool caseSensitive,
        bool wholeWord,
        bool useRegex)
    {
        var results = new List<(int, int)>();
        if (string.IsNullOrEmpty(searchText)) return results;

        var text = document.Text;

        if (useRegex)
        {
            try
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                foreach (Match m in Regex.Matches(text, searchText, options))
                    results.Add((m.Index, m.Length));
            }
            catch { /* invalid regex */ }
        }
        else
        {
            var comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int pos = 0;
            while (pos < text.Length)
            {
                var idx = text.IndexOf(searchText, pos, comparison);
                if (idx < 0) break;

                if (wholeWord && !IsWholeWord(text, idx, searchText.Length))
                {
                    pos = idx + 1;
                    continue;
                }

                results.Add((idx, searchText.Length));
                pos = idx + searchText.Length;
            }
        }

        return results;
    }

    private static bool IsWholeWord(string text, int start, int length)
    {
        if (start > 0 && char.IsLetterOrDigit(text[start - 1])) return false;
        var end = start + length;
        if (end < text.Length && char.IsLetterOrDigit(text[end])) return false;
        return true;
    }
}
