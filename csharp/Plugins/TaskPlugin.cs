using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Services;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// Task tool - spawns isolated subagents for focused subtasks.
///
/// Subagents run in ISOLATED context: they don't see the parent's history.
/// This keeps the main conversation clean while delegating work.
///
/// Agent types:
///   - explore: Read-only (bash, read_file, grep) for searching and analyzing
///   - code: Full access (all tools) for implementation
///   - plan: Read-only for designing strategies
/// </summary>
public class TaskPlugin
{
    private readonly SubAgentRunner _runner;

    public TaskPlugin(SubAgentRunner runner)
    {
        _runner = runner;
    }

    [KernelFunction("Task")]
    [Description(@"Spawn a subagent for a focused subtask. Subagents run in ISOLATED context (they don't see parent's history).

Agent types:
- explore: Read-only agent for searching code, finding files, analyzing structure
- code: Full agent for implementing features and fixing bugs
- plan: Planning agent for designing strategies (read-only)

Example uses:
- Task(explore, ""Find all files using the auth module"")
- Task(plan, ""Design a migration strategy for the database"")
- Task(code, ""Implement the user registration form"")")]
    public async Task<string> SpawnSubAgent(
        [Description("Short task name (3-5 words) for progress display")] string description,
        [Description("Detailed instructions for the subagent")] string prompt,
        [Description("Type of agent: explore, code, or plan")] string agentType)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n> Task: {description} ({agentType})");
        Console.ResetColor();

        return await _runner.RunAsync(description, prompt, agentType);
    }
}
