using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Services;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// Rules tool - load project rules on demand.
///
/// Mirrors Cursor's "Fetch Rules" capability.
/// Agent-decided rules (alwaysApply: false) are listed by description
/// in the system prompt. The agent calls this tool to load the full
/// content when it determines a rule is relevant.
/// </summary>
public class RulesPlugin
{
    private readonly RulesLoader _loader;

    public RulesPlugin(RulesLoader loader)
    {
        _loader = loader;
    }

    [KernelFunction("fetch_rule")]
    [Description(@"Load a project rule by name. Use when a task matches a rule's description.
Rules provide project-specific conventions, coding standards, and workflow instructions.")]
    public string FetchRule(
        [Description("Name of the rule to load")] string ruleName)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n> Loading rule: {ruleName}");
        Console.ResetColor();

        var content = _loader.GetRuleContent(ruleName);

        if (content is null)
        {
            var available = string.Join(", ", _loader.ListRules());
            if (string.IsNullOrEmpty(available))
                available = "none";
            return $"Error: Unknown rule '{ruleName}'. Available: {available}";
        }

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"  Rule loaded ({content.Length} chars)");
        Console.ResetColor();

        return $"""
            <rule-loaded name="{ruleName}">
            {content}
            </rule-loaded>

            Follow the conventions and instructions in the rule above.
            """;
    }
}
