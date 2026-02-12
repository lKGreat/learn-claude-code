namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// Progress update from a running agent.
/// </summary>
public record AgentProgressEvent
{
    /// <summary>ID of the agent reporting progress.</summary>
    public required string AgentId { get; init; }

    /// <summary>Current step number.</summary>
    public int Step { get; init; }

    /// <summary>Elapsed time since agent started.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Human-readable progress message.</summary>
    public required string Message { get; init; }

    /// <summary>Description of the agent task.</summary>
    public string? Description { get; init; }

    /// <summary>Agent type.</summary>
    public string? AgentType { get; init; }
}
