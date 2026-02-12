using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private string _newAgentPrompt = "";

    [ObservableProperty]
    private string _selectedAgentType = "explore";

    [ObservableProperty]
    private AgentItem? _selectedAgent;

    public string[] AvailableAgentTypes { get; } = ["explore", "generalPurpose", "code", "plan"];

    /// <summary>
    /// Fired when user requests to launch a new agent with (type, prompt).
    /// </summary>
    public event Action<string, string>? LaunchAgentRequested;

    /// <summary>
    /// Fired when user requests to resume an agent.
    /// </summary>
    public event Action<string>? ResumeAgentRequested;

    [RelayCommand]
    private void LaunchAgent()
    {
        var prompt = NewAgentPrompt?.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        LaunchAgentRequested?.Invoke(SelectedAgentType, prompt);
        NewAgentPrompt = "";
    }

    [RelayCommand]
    private void ResumeAgent(AgentItem? agent)
    {
        if (agent != null && agent.Status is "Completed" or "Failed")
        {
            ResumeAgentRequested?.Invoke(agent.Id);
        }
    }

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

    public void Clear()
    {
        Agents.Clear();
        RunningCount = 0;
        TotalCount = 0;
    }

    private void UpdateRunningCount()
    {
        RunningCount = Agents.Count(a => a.Status == "Running");
    }
}
