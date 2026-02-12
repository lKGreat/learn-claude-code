using System.Collections.ObjectModel;
using Terminal.Gui;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Displays recent tool call events.
/// </summary>
public class ToolCallPanel : View
{
    private readonly ListView _listView;
    private readonly ObservableCollection<string> _toolLines = [];
    private const int MaxLines = 50;

    public ToolCallPanel()
    {
        Title = "Tool Calls";
        BorderStyle = LineStyle.Rounded;

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _listView.SetSource(_toolLines);
        Add(_listView);
    }

    public void AddToolCall(string functionName, string? args, TimeSpan elapsed, bool success)
    {
        var icon = success ? "✓" : "✗";
        var line = $"{icon} {functionName}";
        if (!string.IsNullOrEmpty(args))
        {
            var shortArgs = args.Length > 40 ? args[..37] + "..." : args;
            line += $" {shortArgs}";
        }
        line += $" [{elapsed.TotalSeconds:F1}s]";

        _toolLines.Add(line);

        while (_toolLines.Count > MaxLines)
            _toolLines.RemoveAt(0);

        if (_toolLines.Count > 0)
            _listView.SelectedItem = _toolLines.Count - 1;

        SetNeedsDraw();
    }

    public void ClearToolCalls()
    {
        _toolLines.Clear();
        SetNeedsDraw();
    }
}
