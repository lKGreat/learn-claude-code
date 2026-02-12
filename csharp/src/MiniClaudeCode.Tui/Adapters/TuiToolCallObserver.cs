using Terminal.Gui;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Tui.Views;

namespace MiniClaudeCode.Tui.Adapters;

/// <summary>
/// IToolCallObserver implementation that updates both the ChatView and ToolCallPanel.
/// </summary>
public class TuiToolCallObserver : IToolCallObserver
{
    private readonly ChatView _chatView;
    private readonly ToolCallPanel _toolCallPanel;

    public TuiToolCallObserver(ChatView chatView, ToolCallPanel toolCallPanel)
    {
        _chatView = chatView;
        _toolCallPanel = toolCallPanel;
    }

    public void OnToolCallStarted(ToolCallEvent toolCall)
    {
        Application.Invoke(() =>
        {
            _toolCallPanel.AddToolCall(toolCall.FunctionName, toolCall.ArgumentSummary, TimeSpan.Zero, true);
        });
    }

    public void OnToolCallCompleted(ToolCallEvent toolCall)
    {
        Application.Invoke(() =>
        {
            _chatView.AddToolCall(toolCall.FunctionName, toolCall.ArgumentSummary, toolCall.Result, toolCall.Elapsed);
            _toolCallPanel.AddToolCall(toolCall.FunctionName, toolCall.ArgumentSummary, toolCall.Elapsed, true);
        });
    }

    public void OnToolCallFailed(ToolCallEvent toolCall, string error)
    {
        Application.Invoke(() =>
        {
            _chatView.AddToolCall(toolCall.FunctionName, toolCall.ArgumentSummary, $"Error: {error}", toolCall.Elapsed);
            _toolCallPanel.AddToolCall(toolCall.FunctionName, toolCall.ArgumentSummary, toolCall.Elapsed, false);
        });
    }
}
