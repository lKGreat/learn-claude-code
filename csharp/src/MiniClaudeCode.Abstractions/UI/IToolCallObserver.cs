using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Abstractions.UI;

/// <summary>
/// Observer for tool call lifecycle events.
/// UI implementations subscribe to display real-time tool call status.
/// </summary>
public interface IToolCallObserver
{
    /// <summary>Called before a tool is invoked.</summary>
    void OnToolCallStarted(ToolCallEvent toolCall);

    /// <summary>Called after a tool completes successfully.</summary>
    void OnToolCallCompleted(ToolCallEvent toolCall);

    /// <summary>Called when a tool call fails.</summary>
    void OnToolCallFailed(ToolCallEvent toolCall, string error);
}
