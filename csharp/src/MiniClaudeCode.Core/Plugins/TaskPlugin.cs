using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Core.Agents;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// Task tool - spawns isolated subagents for focused subtasks.
/// Enhanced to support resumption, model tiers, parallel execution, and read-only mode.
/// </summary>
public class TaskPlugin
{
    private readonly SubAgentRunner _runner;
    private readonly IOutputSink _output;

    public TaskPlugin(SubAgentRunner runner, IOutputSink output)
    {
        _runner = runner;
        _output = output;
    }

    [KernelFunction("Task")]
    [Description(@"Spawn a subagent for a focused subtask. Subagents run in ISOLATED context.

Agent types:
- generalPurpose: General-purpose agent for complex multi-step tasks
- explore: Read-only agent for searching code, finding files, analyzing structure
- code: Full agent for implementing features and fixing bugs
- plan: Planning agent for designing strategies (read-only)

Optional parameters:
- model: 'fast' for quick tasks, 'default' for full capability
- resume: Agent ID to resume a previous agent with preserved context
- readOnly: Restrict write operations
- attachments: Comma-separated file paths to pass as context")]
    public async Task<string> SpawnSubAgent(
        [Description("Short task name (3-5 words)")] string description,
        [Description("Detailed instructions for the subagent")] string prompt,
        [Description("Type of agent: generalPurpose, explore, code, or plan")] string agentType,
        [Description("Model tier: 'fast' or 'default' (optional)")] string? model = null,
        [Description("Agent ID to resume (optional)")] string? resume = null,
        [Description("Restrict write operations (optional)")] bool readOnly = false,
        [Description("Comma-separated file paths to attach (optional)")] string? attachments = null)
    {
        _output.WriteSystem($"Task: {description} ({agentType})");

        var task = new AgentTask
        {
            Description = description,
            Prompt = prompt,
            AgentType = agentType,
            ModelTier = model,
            ResumeAgentId = resume,
            ReadOnly = readOnly,
            Attachments = attachments?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };

        var result = await _runner.RunAsync(task);
        return result.Output;
    }
}
