using System.Collections.ObjectModel;
using Terminal.Gui;
using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Displays real-time status of running, completed, and failed sub-agents.
/// </summary>
public class AgentPanel : View
{
    private readonly ListView _listView;
    private readonly ObservableCollection<string> _agentLines = [];

    public AgentPanel()
    {
        Title = "Agents";
        BorderStyle = LineStyle.Rounded;

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _listView.SetSource(_agentLines);

        Add(_listView);
    }

    public void UpdateAgent(AgentInstanceInfo agent)
    {
        var statusIcon = agent.Status switch
        {
            AgentStatus.Running => "▶",
            AgentStatus.Completed => "✓",
            AgentStatus.Suspended => "⏸",
            AgentStatus.Failed => "✗",
            AgentStatus.Cancelled => "⊘",
            _ => "○"
        };

        var shortId = agent.Id.Length >= 6 ? agent.Id[..6] : agent.Id;
        var line = $"{statusIcon} {agent.AgentType} #{shortId} {agent.Description}";

        var existingIdx = -1;
        for (int i = 0; i < _agentLines.Count; i++)
        {
            if (_agentLines[i].Contains($"#{shortId}"))
            {
                existingIdx = i;
                break;
            }
        }

        if (existingIdx >= 0)
            _agentLines[existingIdx] = line;
        else
            _agentLines.Add(line);

        SetNeedsDraw();
    }

    public void UpdateProgress(AgentProgressEvent progress)
    {
        var shortId = progress.AgentId.Length >= 6 ? progress.AgentId[..6] : progress.AgentId;
        for (int i = 0; i < _agentLines.Count; i++)
        {
            if (_agentLines[i].Contains($"#{shortId}"))
            {
                _agentLines[i] = $"▶ {progress.AgentType ?? "?"} #{shortId} {progress.Message}";
                SetNeedsDraw();
                break;
            }
        }
    }

    public void ClearAgents()
    {
        _agentLines.Clear();
        SetNeedsDraw();
    }
}
