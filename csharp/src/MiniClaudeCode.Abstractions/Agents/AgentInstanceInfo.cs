namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// Tracks the identity and status of a running or completed agent instance.
/// Used by the AgentRegistry to manage agent lifecycles.
/// </summary>
public class AgentInstanceInfo
{
    /// <summary>Unique identifier for this agent instance.</summary>
    public required string Id { get; init; }

    /// <summary>Agent type: generalPurpose, explore, code, plan.</summary>
    public required string AgentType { get; init; }

    /// <summary>Short description of what this agent is doing.</summary>
    public required string Description { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public AgentStatus Status { get; set; } = AgentStatus.Pending;

    /// <summary>When the agent was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the agent last had activity.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>Model tier used (e.g., "fast", "default").</summary>
    public string? ModelTier { get; init; }

    /// <summary>Whether the agent is in read-only mode.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Number of tool calls executed.</summary>
    public int ToolCallCount { get; set; }
}

/// <summary>
/// Agent lifecycle status.
/// </summary>
public enum AgentStatus
{
    /// <summary>Created but not yet started.</summary>
    Pending,

    /// <summary>Currently executing.</summary>
    Running,

    /// <summary>Completed successfully.</summary>
    Completed,

    /// <summary>Failed with an error.</summary>
    Failed,

    /// <summary>Cancelled by user or system.</summary>
    Cancelled,

    /// <summary>Suspended, can be resumed.</summary>
    Suspended
}
