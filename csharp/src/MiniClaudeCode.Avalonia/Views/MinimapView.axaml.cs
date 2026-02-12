using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;

namespace MiniClaudeCode.Avalonia.Views;

/// <summary>
/// Minimap control that renders a scaled overview of the document and allows click-to-scroll.
/// </summary>
public partial class MinimapView : UserControl
{
    private TextEditor? _editor;
    private bool _isDragging;

    // Rendering constants
    private const double LineHeight = 2.0;
    private const double HorizontalPadding = 4.0;
    private const double MaxLineWidth = 52.0;

    // Colors
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#45475A"), 0.6);
    private static readonly IBrush KeywordBrush = new SolidColorBrush(Color.Parse("#89B4FA"), 0.5);
    private static readonly IBrush ViewportBrush = new SolidColorBrush(Color.Parse("#CDD6F4"), 0.08);
    private static readonly IPen ViewportBorderPen = new Pen(new SolidColorBrush(Color.Parse("#585B70"), 0.3), 1);

    public MinimapView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attach the minimap to a TextEditor to track its content and viewport.
    /// </summary>
    public void AttachEditor(TextEditor editor)
    {
        if (_editor != null)
        {
            _editor.TextArea.TextView.ScrollOffsetChanged -= OnScrollChanged;
            _editor.TextChanged -= OnTextChanged;
        }

        _editor = editor;

        if (_editor != null)
        {
            _editor.TextArea.TextView.ScrollOffsetChanged += OnScrollChanged;
            _editor.TextChanged += OnTextChanged;
        }

        InvalidateVisual();
    }

    private void OnScrollChanged(object? sender, EventArgs e) => InvalidateVisual();
    private void OnTextChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_editor?.Document == null) return;

        var doc = _editor.Document;
        var totalLines = doc.LineCount;
        var canvasHeight = Bounds.Height;
        var canvasWidth = Bounds.Width;

        if (totalLines <= 0 || canvasHeight <= 0) return;

        // Scale factor: map all lines to the canvas height
        var scale = Math.Min(LineHeight, canvasHeight / totalLines);

        // Draw text lines as small colored bars
        for (int i = 1; i <= totalLines && (i - 1) * scale < canvasHeight; i++)
        {
            var line = doc.GetLineByNumber(i);
            var lineText = doc.GetText(line.Offset, Math.Min(line.Length, 200));

            if (string.IsNullOrWhiteSpace(lineText)) continue;

            // Calculate visual width based on text length (simplified)
            var trimmed = lineText.TrimStart();
            var indent = lineText.Length - trimmed.Length;
            var textWidth = Math.Min(trimmed.Length * 0.8, MaxLineWidth);
            var xStart = HorizontalPadding + indent * 0.8;
            var yPos = (i - 1) * scale;

            // Simple heuristic: lines starting with keywords get different color
            var brush = IsLikelyKeyword(trimmed) ? KeywordBrush : TextBrush;

            context.DrawRectangle(brush, null,
                new Rect(xStart, yPos, textWidth, Math.Max(scale - 0.5, 0.5)));
        }

        // Draw viewport indicator
        if (_editor.TextArea.TextView.VisualLines.Count > 0)
        {
            var firstVisibleLine = _editor.TextArea.TextView.VisualLines[0].FirstDocumentLine.LineNumber;
            var lastVisibleLine = _editor.TextArea.TextView.VisualLines[^1].LastDocumentLine.LineNumber;

            var viewportTop = (firstVisibleLine - 1) * scale;
            var viewportHeight = (lastVisibleLine - firstVisibleLine + 1) * scale;
            viewportHeight = Math.Max(viewportHeight, 10); // Minimum visible height

            context.DrawRectangle(ViewportBrush, ViewportBorderPen,
                new Rect(0, viewportTop, canvasWidth, viewportHeight));
        }
    }

    private static bool IsLikelyKeyword(string trimmedLine)
    {
        // Simple heuristic for "interesting" lines that might contain declarations
        var keywords = new[]
        {
            "public", "private", "protected", "internal", "class", "interface",
            "struct", "enum", "namespace", "using", "return", "if", "else",
            "for", "foreach", "while", "switch", "case", "function", "const",
            "let", "var", "def", "import", "from", "export"
        };

        foreach (var kw in keywords)
        {
            if (trimmedLine.StartsWith(kw, StringComparison.Ordinal) &&
                (trimmedLine.Length == kw.Length || !char.IsLetterOrDigit(trimmedLine[kw.Length])))
                return true;
        }
        return false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        ScrollToPosition(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            ScrollToPosition(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
    }

    private void ScrollToPosition(Point position)
    {
        if (_editor?.Document == null) return;

        var totalLines = _editor.Document.LineCount;
        var canvasHeight = Bounds.Height;
        if (totalLines <= 0 || canvasHeight <= 0) return;

        var scale = Math.Min(LineHeight, canvasHeight / totalLines);
        var targetLine = (int)(position.Y / scale) + 1;
        targetLine = Math.Clamp(targetLine, 1, totalLines);

        _editor.ScrollToLine(targetLine);
    }
}
