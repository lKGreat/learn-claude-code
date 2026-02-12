namespace MiniClaudeCode.Abstractions.Services;

/// <summary>
/// Loads project rules from .cursor/rules/ and AGENTS.md.
/// </summary>
public interface IRulesLoader
{
    /// <summary>Get all always-apply rules content combined.</summary>
    string GetAlwaysApplyRulesContent();

    /// <summary>Get descriptions of agent-decided rules.</summary>
    string GetAgentDecidedRulesDescriptions();

    /// <summary>Get the content of a specific rule by name.</summary>
    string? GetRuleContent(string name);

    /// <summary>List all available rule names.</summary>
    IReadOnlyList<string> ListRules();

    /// <summary>List agent-decided rule names only.</summary>
    IReadOnlyList<string> ListAgentDecidedRules();
}
