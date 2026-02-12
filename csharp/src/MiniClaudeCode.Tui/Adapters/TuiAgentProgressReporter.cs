using Terminal.Gui;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Tui.Views;

namespace MiniClaudeCode.Tui.Adapters;

/// <summary>
/// IAgentProgressReporter implementation that updates the AgentPanel in the TUI.
/// </summary>
public class TuiAgentProgressReporter : IAgentProgressReporter
{
    private readonly AgentPanel _agentPanel;
    private readonly ChatView _chatView;

    public TuiAgentProgressReporter(AgentPanel agentPanel, ChatView chatView)
    {
        _agentPanel = agentPanel;
        _chatView = chatView;
    }

    public void OnAgentStarted(AgentInstanceInfo agent)
    {
        Application.Invoke(() =>
        {
            _agentPanel.UpdateAgent(agent);
            _chatView.AddSystemMessage($"Agent started: {agent.Description} ({agent.AgentType})");
        });
    }

    public void OnAgentProgress(AgentProgressEvent progress)
    {
        Application.Invoke(() =>
        {
            _agentPanel.UpdateProgress(progress);
        });
    }

    public void OnAgentCompleted(AgentResult result)
    {
        Application.Invoke(() =>
        {
            var info = new AgentInstanceInfo
            {
                Id = result.AgentId,
                AgentType = "completed",
                Description = $"Done ({result.ToolCallCount} tools, {result.Elapsed.TotalSeconds:F1}s)",
                Status = AgentStatus.Completed
            };
            _agentPanel.UpdateAgent(info);
        });
    }

    public void OnAgentFailed(AgentInstanceInfo agent, string error)
    {
        Application.Invoke(() =>
        {
            agent.Status = AgentStatus.Failed;
            _agentPanel.UpdateAgent(agent);
            _chatView.AddSystemMessage($"Agent failed: {agent.Description} - {error}");
        });
    }
}
