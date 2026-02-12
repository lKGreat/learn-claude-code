using System.Collections.Concurrent;
using Microsoft.SemanticKernel.Agents;
using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Core.Agents;

/// <summary>
/// Tracks all agent instances by ID with lifecycle management.
/// Supports agent resumption by preserving ChatHistoryAgentThread.
/// Auto-cleans idle agents after configurable TTL.
/// </summary>
public class AgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentEntry> _agents = new();
    private readonly TimeSpan _idleTtl;
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Internal storage for a registered agent.
    /// </summary>
    private record AgentEntry(
        AgentInstanceInfo Info,
        ChatHistoryAgentThread Thread,
        ChatCompletionAgent Agent);

    public AgentRegistry(TimeSpan? idleTtl = null)
    {
        _idleTtl = idleTtl ?? TimeSpan.FromMinutes(30);
        // Run cleanup every 5 minutes
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Register a new agent and return its ID.
    /// </summary>
    public string Register(AgentInstanceInfo info, ChatHistoryAgentThread thread, ChatCompletionAgent agent)
    {
        var entry = new AgentEntry(info, thread, agent);
        _agents[info.Id] = entry;
        return info.Id;
    }

    /// <summary>
    /// Try to get an agent entry by ID (for resumption).
    /// </summary>
    public bool TryGet(string agentId, out AgentInstanceInfo? info, out ChatHistoryAgentThread? thread, out ChatCompletionAgent? agent)
    {
        if (_agents.TryGetValue(agentId, out var entry))
        {
            entry.Info.LastActivityAt = DateTime.UtcNow;
            info = entry.Info;
            thread = entry.Thread;
            agent = entry.Agent;
            return true;
        }

        info = null;
        thread = null;
        agent = null;
        return false;
    }

    /// <summary>
    /// Update the status of an agent.
    /// </summary>
    public void UpdateStatus(string agentId, AgentStatus status)
    {
        if (_agents.TryGetValue(agentId, out var entry))
        {
            entry.Info.Status = status;
            entry.Info.LastActivityAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Increment tool call count for an agent.
    /// </summary>
    public void IncrementToolCalls(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var entry))
        {
            entry.Info.ToolCallCount++;
            entry.Info.LastActivityAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Get all currently running agents.
    /// </summary>
    public IReadOnlyList<AgentInstanceInfo> GetRunningAgents()
    {
        return _agents.Values
            .Where(e => e.Info.Status == AgentStatus.Running)
            .Select(e => e.Info)
            .ToList();
    }

    /// <summary>
    /// Get all agents (any status).
    /// </summary>
    public IReadOnlyList<AgentInstanceInfo> GetAllAgents()
    {
        return _agents.Values.Select(e => e.Info).ToList();
    }

    /// <summary>
    /// Remove an agent from the registry.
    /// </summary>
    public void Remove(string agentId)
    {
        _agents.TryRemove(agentId, out _);
    }

    /// <summary>
    /// Clean up expired (idle) agents.
    /// </summary>
    private void CleanupExpired(object? state)
    {
        var cutoff = DateTime.UtcNow - _idleTtl;
        var expired = _agents.Values
            .Where(e => e.Info.Status is AgentStatus.Completed or AgentStatus.Failed or AgentStatus.Cancelled
                     && e.Info.LastActivityAt < cutoff)
            .Select(e => e.Info.Id)
            .ToList();

        foreach (var id in expired)
            _agents.TryRemove(id, out _);
    }

    /// <summary>
    /// Total number of registered agents.
    /// </summary>
    public int Count => _agents.Count;
}
