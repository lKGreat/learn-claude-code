using Terminal.Gui;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Tui.Views;

namespace MiniClaudeCode.Tui.Adapters;

/// <summary>
/// IOutputSink implementation that writes to the TUI ChatView.
/// Uses Application.Invoke to marshal calls to the UI thread.
/// </summary>
public class TuiOutputSink : IOutputSink
{
    private readonly ChatView _chatView;

    public TuiOutputSink(ChatView chatView)
    {
        _chatView = chatView;
    }

    public void WriteAssistant(string content)
    {
        Application.Invoke(() => _chatView.AddAssistantMessage(content));
    }

    public void WriteSystem(string message)
    {
        Application.Invoke(() => _chatView.AddSystemMessage(message));
    }

    public void WriteError(string message)
    {
        Application.Invoke(() => _chatView.AddSystemMessage($"[Error] {message}"));
    }

    public void WriteWarning(string message)
    {
        Application.Invoke(() => _chatView.AddSystemMessage($"[Warning] {message}"));
    }

    public void WriteDebug(string message)
    {
        // Debug messages go to chat as system messages (could be toggled)
        Application.Invoke(() => _chatView.AddSystemMessage(message));
    }

    public void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
            Application.Invoke(() => _chatView.AddSystemMessage(text));
    }

    public void Clear()
    {
        Application.Invoke(() => _chatView.ClearChat());
    }
}
