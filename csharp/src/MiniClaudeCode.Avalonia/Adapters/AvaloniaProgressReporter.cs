using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Adapters;

/// <summary>
/// Implements IAgentProgressReporter by updating the AgentPanelViewModel.
/// </summary>
public class AvaloniaProgressReporter(AgentPanelViewModel agentPanel, ChatViewModel chat)
    : IAgentProgressReporter
{
    public void OnAgentStarted(AgentInstanceInfo agent)
    {
        DispatcherService.Post(() =>
        {
            agentPanel.AddAgent(new AgentItem
            {
                Id = agent.Id,
                AgentType = agent.AgentType,
                Description = agent.Description,
                Status = "Running",
                StartTime = agent.CreatedAt
            });

            chat.AddSystemMessage($"[Agent] Started {agent.AgentType}: {agent.Description}");
        });
    }

    public void OnAgentProgress(AgentProgressEvent progress)
    {
        DispatcherService.Post(() =>
        {
            agentPanel.UpdateAgent(progress.AgentId, a =>
            {
                a.Elapsed = FormatElapsed(progress.Elapsed);
            });
        });
    }

    public void OnAgentCompleted(AgentResult result)
    {
        DispatcherService.Post(() =>
        {
            agentPanel.UpdateAgent(result.AgentId, a =>
            {
                a.Status = "Completed";
                a.Elapsed = FormatElapsed(result.Elapsed);
                a.ToolCallCount = result.ToolCallCount;
                a.Result = result.Output.Length > 200
                    ? result.Output[..200] + "..."
                    : result.Output;
            });
        });
    }

    public void OnAgentFailed(AgentInstanceInfo agent, string error)
    {
        DispatcherService.Post(() =>
        {
            agentPanel.UpdateAgent(agent.Id, a =>
            {
                a.Status = "Failed";
                a.Result = error;
            });

            chat.AddErrorMessage($"[Agent] {agent.AgentType} failed: {error}");
        });
    }

    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts.TotalMinutes < 1)
            return $"0:{ts.Seconds:D2}";
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }
}
