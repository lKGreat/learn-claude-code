using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Abstractions.UI;

/// <summary>
/// Reports agent progress updates to the UI.
/// Used by subagent runner and agent team to stream status.
/// </summary>
public interface IAgentProgressReporter
{
    /// <summary>Report that an agent has started.</summary>
    void OnAgentStarted(AgentInstanceInfo agent);

    /// <summary>Report progress update from an agent.</summary>
    void OnAgentProgress(AgentProgressEvent progress);

    /// <summary>Report that an agent has completed.</summary>
    void OnAgentCompleted(AgentResult result);

    /// <summary>Report that an agent has failed.</summary>
    void OnAgentFailed(AgentInstanceInfo agent, string error);
}
