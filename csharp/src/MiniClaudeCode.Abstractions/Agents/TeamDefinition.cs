namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// Defines a coordinated multi-agent workflow (Agent Team).
/// </summary>
public record TeamDefinition
{
    /// <summary>Human-readable name of the team workflow.</summary>
    public required string Name { get; init; }

    /// <summary>Description of what this team does.</summary>
    public string? Description { get; init; }

    /// <summary>Execution pattern for the team.</summary>
    public required TeamPattern Pattern { get; init; }

    /// <summary>Ordered list of agent roles in this team.</summary>
    public required IReadOnlyList<TeamRole> Roles { get; init; }
}

/// <summary>
/// A role within an agent team.
/// </summary>
public record TeamRole
{
    /// <summary>Name of this role (e.g., "planner", "explorer", "coder").</summary>
    public required string Name { get; init; }

    /// <summary>Agent type to use for this role.</summary>
    public required string AgentType { get; init; }

    /// <summary>Prompt template. Use {input} for the team input, {previous} for prior role output.</summary>
    public required string PromptTemplate { get; init; }

    /// <summary>Model tier override for this role.</summary>
    public string? ModelTier { get; init; }

    /// <summary>Whether this role is read-only.</summary>
    public bool ReadOnly { get; init; }
}

/// <summary>
/// Execution patterns for agent teams.
/// </summary>
public enum TeamPattern
{
    /// <summary>Roles execute one after another, each receiving the previous role's output.</summary>
    Sequential,

    /// <summary>All roles execute in parallel, results are merged.</summary>
    FanOutFanIn,

    /// <summary>First role acts as supervisor, delegating to other roles.</summary>
    Supervisor
}
