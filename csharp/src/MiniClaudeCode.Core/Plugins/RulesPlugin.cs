using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.Services;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// Rules tool - load project rules on demand.
/// </summary>
public class RulesPlugin
{
    private readonly IRulesLoader _loader;
    private readonly IOutputSink _output;

    public RulesPlugin(IRulesLoader loader, IOutputSink output)
    {
        _loader = loader;
        _output = output;
    }

    [KernelFunction("fetch_rule")]
    [Description(@"Load a project rule by name. Use when a task matches a rule's description.")]
    public string FetchRule(
        [Description("Name of the rule to load")] string ruleName)
    {
        _output.WriteDebug($"Loading rule: {ruleName}");

        var content = _loader.GetRuleContent(ruleName);

        if (content is null)
        {
            var available = string.Join(", ", _loader.ListRules());
            if (string.IsNullOrEmpty(available)) available = "none";
            return $"Error: Unknown rule '{ruleName}'. Available: {available}";
        }

        _output.WriteDebug($"Rule loaded ({content.Length} chars)");

        return $"""
            <rule-loaded name="{ruleName}">
            {content}
            </rule-loaded>

            Follow the conventions and instructions in the rule above.
            """;
    }
}
