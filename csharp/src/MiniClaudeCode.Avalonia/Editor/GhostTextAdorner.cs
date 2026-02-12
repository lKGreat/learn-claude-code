#nullable enable

using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace MiniClaudeCode.Avalonia.Editor;

/// <summary>
/// Renders AI completion suggestions as gray italic "ghost text" at the cursor position in AvaloniaEdit.
/// This adorner displays completion suggestions inline without modifying the document.
///
/// Usage:
/// <code>
/// var ghostText = new GhostTextAdorner(textEditor.TextArea);
/// ghostText.SetGhostText("completion text", caretOffset);
/// textEditor.TextArea.TextView.BackgroundRenderers.Add(ghostText);
///
/// // To accept:
/// ghostText.Accepted += () => { /* insert ghost text */ };
///
/// // To dismiss:
/// ghostText.Clear();
/// </code>
/// </summary>
public class GhostTextAdorner : IBackgroundRenderer
{
    private readonly TextArea _textArea;
    private string? _ghostText;
    private int _ghostTextOffset;
    private bool _isVisible;
    private readonly FontFamily _fontFamily;
    private readonly double _fontSize;

    // Visual styling
    private static readonly Color GhostColor = Color.Parse("#6C7086"); // Gray from Catppuccin Mocha
    private const double GhostOpacity = 0.6;

    /// <summary>
    /// Initializes a new instance of the GhostTextAdorner.
    /// </summary>
    /// <param name="textArea">The TextArea to render ghost text in.</param>
    /// <param name="fontFamily">Font family for ghost text (optional, uses Cascadia Code if null).</param>
    /// <param name="fontSize">Font size for ghost text (optional, uses 13 if not specified).</param>
    public GhostTextAdorner(TextArea textArea, FontFamily? fontFamily = null, double fontSize = 13)
    {
        _textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
        _fontFamily = fontFamily ?? FontFamily.Parse("Cascadia Code, Consolas, Menlo, monospace");
        _fontSize = fontSize;
    }

    /// <summary>
    /// Gets or sets the ghost text to display. Set to null to hide.
    /// </summary>
    public string? GhostText
    {
        get => _ghostText;
        set
        {
            if (_ghostText != value)
            {
                _ghostText = value;
                _isVisible = !string.IsNullOrEmpty(value);
                _textArea.TextView.InvalidateLayer(Layer);
            }
        }
    }

    /// <summary>
    /// Gets the rendering layer (draws on top of text).
    /// </summary>
    public KnownLayer Layer => KnownLayer.Caret;

    /// <summary>
    /// Fired when the user accepts the ghost text (e.g., presses Tab).
    /// </summary>
    public event Action? Accepted;

    /// <summary>
    /// Fired when the ghost text is dismissed (e.g., user types or moves cursor).
    /// </summary>
    public event Action? Dismissed;

    /// <summary>
    /// Sets the ghost text and its position in the document.
    /// </summary>
    /// <param name="text">The ghost text to display.</param>
    /// <param name="offset">The document offset where the ghost text should appear.</param>
    public void SetGhostText(string? text, int offset)
    {
        _ghostText = text;
        _ghostTextOffset = offset;
        _isVisible = !string.IsNullOrEmpty(text);
        _textArea.TextView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Sets the position where ghost text starts.
    /// </summary>
    /// <param name="offset">The document offset.</param>
    public void SetPosition(int offset)
    {
        _ghostTextOffset = offset;
        if (_isVisible)
        {
            _textArea.TextView.InvalidateLayer(Layer);
        }
    }

    /// <summary>
    /// Clears and hides the ghost text.
    /// </summary>
    public void Clear()
    {
        if (_isVisible)
        {
            _ghostText = null;
            _isVisible = false;
            _textArea.TextView.InvalidateLayer(Layer);
            Dismissed?.Invoke();
        }
    }

    /// <summary>
    /// Accepts the ghost text (fires the Accepted event).
    /// </summary>
    public void Accept()
    {
        if (_isVisible)
        {
            Accepted?.Invoke();
            Clear();
        }
    }

    /// <summary>
    /// Renders the ghost text on the TextView.
    /// </summary>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!_isVisible || string.IsNullOrEmpty(_ghostText) || textView.Document == null)
            return;

        // Validate offset is within document bounds
        if (_ghostTextOffset < 0 || _ghostTextOffset > textView.Document.TextLength)
            return;

        try
        {
            // Get the document location from offset
            var location = textView.Document.GetLocation(_ghostTextOffset);

            // Find the visual line that contains this offset
            var visualLine = textView.VisualLines.FirstOrDefault(vl =>
                vl.FirstDocumentLine.LineNumber <= location.Line &&
                location.Line <= vl.LastDocumentLine.LineNumber);

            if (visualLine == null)
                return; // Offset is not in a currently visible line

            // Get the visual position within the line
            var visualColumn = visualLine.GetVisualColumn(_ghostTextOffset - visualLine.FirstDocumentLine.Offset);
            var textLine = visualLine.GetTextLine(visualColumn);

            // Calculate the X position based on the column
            var xPos = visualLine.GetVisualPosition(visualColumn, VisualYPosition.LineTop).X;

            // Calculate the Y position
            var yPos = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineTop);

            // Split ghost text into lines
            var lines = _ghostText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Get font and styling
            var typeface = new Typeface(_fontFamily, FontStyle.Italic, FontWeight.Normal);
            var foreground = new SolidColorBrush(GhostColor, GhostOpacity);

            // Calculate line height using FormattedText
            var lineHeight = CalculateLineHeight();

            // Render each line
            var currentY = yPos;
            var currentX = xPos;
            var isFirstLine = true;

            foreach (var line in lines)
            {
                // For subsequent lines, we need to move to the next line and reset X
                // For now, we'll just render on the same line (multi-line support can be enhanced later)
                if (!isFirstLine)
                {
                    currentY += lineHeight;
                    currentX = xPos; // Could calculate proper indentation here
                }

                // Create formatted text for this line
                if (!string.IsNullOrEmpty(line))
                {
                    var formattedText = new FormattedText(
                        line,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _fontSize,
                        foreground);

                    // Draw the text at the calculated position
                    drawingContext.DrawText(formattedText, new Point(currentX, currentY));
                }

                isFirstLine = false;
            }
        }
        catch
        {
            // Silently fail rendering if something goes wrong (e.g., document changed during render)
            // This prevents crashes during scrolling or rapid text changes
        }
    }

    /// <summary>
    /// Calculates the line height based on font metrics.
    /// </summary>
    private double CalculateLineHeight()
    {
        // Use FormattedText to get the actual height for the font
        var testText = new FormattedText(
            "Mg", // Use characters with ascenders and descenders
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(_fontFamily),
            _fontSize,
            Brushes.Black);

        // Return height with typical line spacing (1.2x height is common)
        return testText.Height * 1.2;
    }
}
