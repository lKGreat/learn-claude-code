using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Adapters;

/// <summary>
/// Implements IToolCallObserver by updating the ToolCallPanelViewModel.
/// </summary>
public class AvaloniaToolCallObserver(ToolCallPanelViewModel toolCallPanel, ChatViewModel chat)
    : IToolCallObserver
{
    public void OnToolCallStarted(ToolCallEvent toolCall)
    {
        DispatcherService.Post(() =>
        {
            toolCallPanel.AddToolCall(new ToolCallItem
            {
                ToolName = toolCall.FunctionName,
                ArgumentsPreview = toolCall.ArgumentSummary ?? "",
                FullArguments = toolCall.ArgumentSummary ?? "",
                Status = "Running"
            });
        });
    }

    public void OnToolCallCompleted(ToolCallEvent toolCall)
    {
        DispatcherService.Post(() =>
        {
            // Update the most recent matching running tool call
            toolCallPanel.CompleteLatest(
                toolCall.FunctionName,
                toolCall.Result ?? "",
                toolCall.Elapsed);
        });
    }

    public void OnToolCallFailed(ToolCallEvent toolCall, string error)
    {
        DispatcherService.Post(() =>
        {
            toolCallPanel.FailLatest(toolCall.FunctionName, error, toolCall.Elapsed);
            chat.AddWarningMessage($"[Tool] {toolCall.FunctionName} failed: {error}");
        });
    }
}
