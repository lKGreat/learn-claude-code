using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace MiniClaudeCode.Avalonia.Editor.Rendering;

/// <summary>
/// Background renderer that draws vertical indent guide lines at each tab stop.
/// </summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private readonly IPen _pen;
    private int _tabSize = 4;

    public KnownLayer Layer => KnownLayer.Background;

    public int TabSize
    {
        get => _tabSize;
        set => _tabSize = Math.Max(1, value);
    }

    public IndentGuideRenderer()
    {
        _pen = new Pen(new SolidColorBrush(Color.Parse("#313244"), 0.4), 1,
            new DashStyle([2, 2], 0));
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView.Document == null) return;

        // 文档切换/布局过渡期 VisualLines 可能短暂不一致，避免抛异常（first-chance）
        try
        {
            var charWidth = textView.WideSpaceWidth;
            if (charWidth <= 0) return;

            foreach (var visualLine in textView.VisualLines)
            {
                var lineNumber = visualLine.FirstDocumentLine.LineNumber;
                var line = textView.Document.GetLineByNumber(lineNumber);
                var lineText = textView.Document.GetText(line.Offset, line.Length);

                // Count leading whitespace to determine indent level
                var indentLevel = GetIndentLevel(lineText);

                // Also check adjacent lines for deeper indents (empty lines should show guides
                // based on surrounding context)
                if (string.IsNullOrWhiteSpace(lineText))
                {
                    indentLevel = GetContextIndent(textView.Document, lineNumber);
                }

                // Draw vertical guides for each indent level
                for (int level = 1; level <= indentLevel; level++)
                {
                    var xPos = charWidth * level * _tabSize;

                    var yTop = visualLine.GetTextLineVisualYPosition(
                        visualLine.TextLines[0], VisualYPosition.LineTop);
                    var yBottom = visualLine.GetTextLineVisualYPosition(
                        visualLine.TextLines[^1], VisualYPosition.LineBottom);

                    // Adjust for scroll offset
                    yTop -= textView.VerticalOffset;
                    yBottom -= textView.VerticalOffset;

                    drawingContext.DrawLine(_pen, new Point(xPos, yTop), new Point(xPos, yBottom));
                }
            }
        }
        catch
        {
            // ignore: best-effort rendering
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

    private int GetContextIndent(TextDocument document, int lineNumber)
    {
        // Look at surrounding lines to determine the indent level for blank lines
        int maxIndent = 0;

        // Check line above
        if (lineNumber > 1)
        {
            var prevLine = document.GetLineByNumber(lineNumber - 1);
            var prevText = document.GetText(prevLine.Offset, prevLine.Length);
            if (!string.IsNullOrWhiteSpace(prevText))
                maxIndent = Math.Max(maxIndent, GetIndentLevel(prevText));
        }

        // Check line below
        if (lineNumber < document.LineCount)
        {
            var nextLine = document.GetLineByNumber(lineNumber + 1);
            var nextText = document.GetText(nextLine.Offset, nextLine.Length);
            if (!string.IsNullOrWhiteSpace(nextText))
                maxIndent = Math.Max(maxIndent, GetIndentLevel(nextText));
        }

        return maxIndent;
    }
}
