namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// Defines a task to be executed by a sub-agent.
/// This is the input for the ParallelAgentExecutor and SubAgentRunner.
/// </summary>
public record AgentTask
{
    /// <summary>Short task name (3-5 words) for progress display.</summary>
    public required string Description { get; init; }

    /// <summary>Detailed instructions for the sub-agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Type of agent: generalPurpose, explore, code, plan.</summary>
    public required string AgentType { get; init; }

    /// <summary>Model tier: "fast", "default", or a specific model ID.</summary>
    public string? ModelTier { get; init; }

    /// <summary>If set, resume this existing agent by ID.</summary>
    public string? ResumeAgentId { get; init; }

    /// <summary>If true, restrict write tools.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>File paths to pass as context to the agent.</summary>
    public IReadOnlyList<string>? Attachments { get; init; }
}
