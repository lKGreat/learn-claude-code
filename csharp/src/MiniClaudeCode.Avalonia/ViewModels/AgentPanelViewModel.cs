using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the agent status panel.
/// </summary>
public partial class AgentPanelViewModel : ObservableObject
{
    public ObservableCollection<AgentItem> Agents { get; } = [];

    [ObservableProperty]
    private int _runningCount;

    [ObservableProperty]
    private int _totalCount;

    public void AddAgent(AgentItem agent)
    {
        Agents.Insert(0, agent);
        TotalCount = Agents.Count;
        UpdateRunningCount();
    }

    public AgentItem? FindAgent(string id)
    {
        return Agents.FirstOrDefault(a => a.Id == id);
    }

    public void UpdateAgent(string id, Action<AgentItem> update)
    {
        var agent = FindAgent(id);
        if (agent != null)
        {
            update(agent);
            UpdateRunningCount();
        }
    }

    private void UpdateRunningCount()
    {
        RunningCount = Agents.Count(a => a.Status == "Running");
    }
}
