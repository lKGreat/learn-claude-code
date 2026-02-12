using Terminal.Gui;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Multi-line input area for user messages.
/// Enter sends, Shift+Enter for newline.
/// </summary>
public class InputView : View
{
    private readonly TextView _editor;

    /// <summary>
    /// Fired when the user submits input (presses Enter without Shift).
    /// </summary>
    public event Action<string>? InputSubmitted;

    public InputView()
    {
        Title = "Input (Enter=send, Shift+Enter=newline)";
        BorderStyle = LineStyle.Rounded;

        _editor = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WordWrap = true,
            AllowsTab = false,
        };

        _editor.KeyDown += OnKeyDown;
        Add(_editor);
    }

    private void OnKeyDown(object? sender, Key e)
    {
        if (e.KeyCode == KeyCode.Enter && !e.IsShift)
        {
            e.Handled = true;
            var text = _editor.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                InputSubmitted?.Invoke(text);
                _editor.Text = "";
            }
        }
    }

    /// <summary>
    /// Focus the input editor.
    /// </summary>
    public void FocusInput()
    {
        _editor.SetFocus();
    }

    /// <summary>
    /// Set the input text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        _editor.Text = text;
    }
}
