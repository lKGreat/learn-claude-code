using Terminal.Gui;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Scrollable chat history view displaying user messages, agent responses, and tool calls.
/// </summary>
public class ChatView : View
{
    private readonly List<ChatEntry> _entries = [];
    private readonly TextView _textView;

    public ChatView()
    {
        Title = "Chat";
        BorderStyle = LineStyle.Rounded;

        _textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            AllowsTab = false,
        };

        Add(_textView);
    }

    public void AddUserMessage(string message)
    {
        _entries.Add(new ChatEntry(ChatRole.User, message));
        RefreshDisplay();
    }

    public void AddAssistantMessage(string message)
    {
        _entries.Add(new ChatEntry(ChatRole.Assistant, message));
        RefreshDisplay();
    }

    public void AddSystemMessage(string message)
    {
        _entries.Add(new ChatEntry(ChatRole.System, message));
        RefreshDisplay();
    }

    public void AddToolCall(string functionName, string? args, string? result, TimeSpan elapsed)
    {
        var text = $"  > {functionName}";
        if (!string.IsNullOrEmpty(args))
            text += $" {args}";
        text += $" [{elapsed.TotalSeconds:F1}s]";
        if (!string.IsNullOrEmpty(result))
        {
            var truncated = result.Length > 200 ? result[..200] + "..." : result;
            text += $"\n    {truncated}";
        }
        _entries.Add(new ChatEntry(ChatRole.Tool, text));
        RefreshDisplay();
    }

    public void ClearChat()
    {
        _entries.Clear();
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        var lines = new List<string>();

        foreach (var entry in _entries)
        {
            var prefix = entry.Role switch
            {
                ChatRole.User => "[User] ",
                ChatRole.Assistant => "[Agent] ",
                ChatRole.System => "[System] ",
                ChatRole.Tool => "",
                _ => ""
            };

            lines.Add(prefix + entry.Content);
            lines.Add(""); // blank line between entries
        }

        _textView.Text = string.Join("\n", lines);

        // Auto-scroll to bottom
        if (_textView.Text.Length > 0)
        {
            _textView.MoveEnd();
        }
    }

    private record ChatEntry(ChatRole Role, string Content);

    private enum ChatRole { User, Assistant, System, Tool }
}
