namespace MiniClaudeCode.Abstractions.Agents;

/// <summary>
/// Represents a tool invocation event (before, after, or error).
/// </summary>
public record ToolCallEvent
{
    /// <summary>Name of the function being called.</summary>
    public required string FunctionName { get; init; }

    /// <summary>Plugin that owns the function.</summary>
    public string? PluginName { get; init; }

    /// <summary>Human-readable summary of the key arguments.</summary>
    public string? ArgumentSummary { get; init; }

    /// <summary>The result text (populated after completion).</summary>
    public string? Result { get; set; }

    /// <summary>Elapsed time (populated after completion).</summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>Whether this call succeeded.</summary>
    public bool Success { get; set; } = true;
}
