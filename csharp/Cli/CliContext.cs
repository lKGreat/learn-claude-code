using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Services;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Shared context passed to slash commands and REPL components.
/// Holds references to all services needed during an interactive session.
/// </summary>
public class CliContext
{
    // --- Core ---
    public required Kernel Kernel { get; set; }
    public required ChatCompletionAgent Agent { get; set; }
    public required ChatHistoryAgentThread Thread { get; set; }

    // --- Configuration ---
    public required string WorkDir { get; set; }
    public required ModelProviderConfig ActiveConfig { get; set; }
    public required Dictionary<ModelProvider, ModelProviderConfig> ProviderConfigs { get; set; }
    public required Dictionary<string, ModelProvider> AgentProviderOverrides { get; set; }

    // --- Services ---
    public required SkillLoader SkillLoader { get; set; }
    public required RulesLoader RulesLoader { get; set; }
    public required TodoManager TodoManager { get; set; }
    public required SubAgentRunner SubAgentRunner { get; set; }

    // --- State ---
    public int TurnCount { get; set; }
    public int TotalToolCalls { get; set; }
    public bool ExitRequested { get; set; }

    /// <summary>
    /// Reset the conversation thread (for /new command).
    /// </summary>
    public void ResetThread()
    {
        Thread = new ChatHistoryAgentThread();
        TurnCount = 0;
        TotalToolCalls = 0;
    }
}
