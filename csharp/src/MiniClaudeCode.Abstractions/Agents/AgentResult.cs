namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// The result returned when an agent completes its task.
/// </summary>
public record AgentResult
{
    /// <summary>ID of the agent that produced this result.</summary>
    public required string AgentId { get; init; }

    /// <summary>The agent's output text.</summary>
    public required string Output { get; init; }

    /// <summary>Number of tool calls executed during the run.</summary>
    public int ToolCallCount { get; init; }

    /// <summary>Total elapsed time.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Whether the result is an error.</summary>
    public bool IsError { get; init; }

    /// <summary>Error message if IsError is true.</summary>
    public string? ErrorMessage { get; init; }
}
