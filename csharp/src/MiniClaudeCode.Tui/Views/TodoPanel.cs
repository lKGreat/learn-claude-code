using Terminal.Gui;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Displays the current todo list from TodoManager.
/// </summary>
public class TodoPanel : View
{
    private readonly TextView _textView;

    public TodoPanel()
    {
        Title = "Todos";
        BorderStyle = LineStyle.Rounded;

        _textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
        };

        _textView.Text = "(no todos)";
        Add(_textView);
    }

    public void UpdateTodos(string rendered)
    {
        _textView.Text = rendered;
        SetNeedsDraw();
    }
}
