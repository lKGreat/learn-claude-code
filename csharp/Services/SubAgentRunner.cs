using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Plugins;
using MiniClaudeCode.Services.Providers;

namespace MiniClaudeCode.Services;

/// <summary>
/// Creates and runs isolated subagents for focused subtasks.
///
/// Each subagent has:
///   1. Its own fresh ChatHistory (no parent context!)
///   2. Filtered tools based on agent type (explore = read-only, code = full)
///   3. Specialized system prompt
///   4. Its own model provider (configurable per agent type)
///   5. Returns only final text summary to parent
///
/// This is the core of the subagent mechanism:
///   Process isolation = Context isolation
/// </summary>
public class SubAgentRunner
{
    private readonly Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs;
    private readonly ModelProvider _defaultProvider;
    private readonly Dictionary<string, ModelProvider> _agentProviderOverrides;
    private readonly string _workDir;

    /// <summary>
    /// Agent type configurations.
    /// Each type has a description, allowed tool set, and specialized prompt.
    /// </summary>
    private static readonly Dictionary<string, AgentTypeConfig> AgentTypes = new()
    {
        ["explore"] = new(
            Description: "Read-only agent for exploring code, finding files, searching",
            Tools: ["bash", "read_file", "grep", "glob", "list_directory"],
            Prompt: "You are an exploration agent. Search and analyze, but never modify files. Return a concise summary."
        ),
        ["code"] = new(
            Description: "Full agent for implementing features and fixing bugs",
            Tools: ["*"], // All tools from all plugins
            Prompt: "You are a coding agent. Implement the requested changes efficiently."
        ),
        ["plan"] = new(
            Description: "Planning agent for designing implementation strategies",
            Tools: ["bash", "read_file", "grep", "glob", "list_directory"],
            Prompt: "You are a planning agent. Analyze the codebase and output a numbered implementation plan. Do NOT make changes."
        ),
    };

    /// <summary>
    /// Create a SubAgentRunner with multi-provider support.
    /// </summary>
    /// <param name="providerConfigs">All available provider configurations.</param>
    /// <param name="defaultProvider">Default provider for agents without overrides.</param>
    /// <param name="agentProviderOverrides">Per-agent-type provider overrides (e.g., explore -> DeepSeek).</param>
    /// <param name="workDir">Working directory for file operations.</param>
    public SubAgentRunner(
        Dictionary<ModelProvider, ModelProviderConfig> providerConfigs,
        ModelProvider defaultProvider,
        Dictionary<string, ModelProvider> agentProviderOverrides,
        string workDir)
    {
        _providerConfigs = providerConfigs;
        _defaultProvider = defaultProvider;
        _agentProviderOverrides = agentProviderOverrides;
        _workDir = workDir;
    }

    /// <summary>
    /// Get the provider config for a given agent type.
    /// Uses override if configured, otherwise falls back to default.
    /// </summary>
    private ModelProviderConfig GetProviderForAgent(string agentType)
    {
        if (_agentProviderOverrides.TryGetValue(agentType, out var overrideProvider)
            && _providerConfigs.TryGetValue(overrideProvider, out var overrideConfig))
        {
            return overrideConfig;
        }
        return _providerConfigs[_defaultProvider];
    }

    /// <summary>
    /// Get formatted descriptions of all available agent types.
    /// </summary>
    public static string GetAgentDescriptions()
    {
        return string.Join("\n", AgentTypes.Select(kv =>
            $"- {kv.Key}: {kv.Value.Description}"));
    }

    /// <summary>
    /// Get the list of valid agent type names.
    /// </summary>
    public static IEnumerable<string> GetAgentTypeNames() => AgentTypes.Keys;

    /// <summary>
    /// Run a subagent task with isolated context.
    ///
    /// 1. Create a new Kernel with filtered plugins
    /// 2. Create a new ChatCompletionAgent with agent-specific system prompt
    /// 3. Run it on its own ChatHistoryAgentThread
    /// 4. Return ONLY the final text (parent sees just the summary)
    /// </summary>
    public async Task<string> RunAsync(string description, string prompt, string agentType)
    {
        if (!AgentTypes.TryGetValue(agentType, out var config))
            return $"Error: Unknown agent type '{agentType}'. Available: {string.Join(", ", AgentTypes.Keys)}";

        // Resolve which provider this agent type should use
        var providerConfig = GetProviderForAgent(agentType);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"  [{agentType}@{providerConfig.Provider}] {description}");
        Console.ResetColor();

        var sw = Stopwatch.StartNew();
        int toolCount = 0;

        try
        {
            // Build an isolated kernel with filtered plugins
            var builder = Kernel.CreateBuilder();

            // Register the correct provider's chat completion service
            RegisterChatCompletion(builder, providerConfig);

            // Create plugins for this subagent
            var codingPlugin = new CodingPlugin(_workDir);
            var fileSearchPlugin = new FileSearchPlugin(_workDir);
            var codingKernelPlugin = KernelPluginFactory.CreateFromObject(codingPlugin, "CodingPlugin");
            var fileSearchKernelPlugin = KernelPluginFactory.CreateFromObject(fileSearchPlugin, "FileSearchPlugin");

            // Combine all functions into a single list for filtering
            var allFunctions = codingKernelPlugin
                .Concat(fileSearchKernelPlugin)
                .ToList();

            // Filter tools based on agent type
            if (config.Tools.Contains("*"))
            {
                // Full access - all tools
                builder.Plugins.Add(codingKernelPlugin);
                builder.Plugins.Add(fileSearchKernelPlugin);
            }
            else
            {
                // Filtered - only allowed tools from all plugins
                var filteredFunctions = allFunctions
                    .Where(f => config.Tools.Contains(f.Name))
                    .ToList();

                var filteredPlugin = KernelPluginFactory.CreateFromFunctions(
                    "Tools",
                    filteredFunctions);

                builder.Plugins.Add(filteredPlugin);
            }

            var kernel = builder.Build();

            // Agent-specific system prompt
            var systemPrompt = $"""
                You are a {agentType} subagent at {_workDir}.

                {config.Prompt}

                Complete the task and return a clear, concise summary.
                """;

            // Create isolated agent
            var agent = new ChatCompletionAgent
            {
                Name = $"SubAgent_{agentType}",
                Instructions = systemPrompt,
                Kernel = kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
            };

            // Fresh thread (isolated context - this is the key!)
            var thread = new ChatHistoryAgentThread();
            var userMessage = new ChatMessageContent(AuthorRole.User, prompt);

            string finalText = "";

            // Invoke the agent - SK handles the tool-calling loop
            await foreach (var response in agent.InvokeAsync(userMessage, thread))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                    finalText = response.Message.Content;

                // Count tool invocations by checking for function results
                toolCount++;

                // Progress update
                Console.Write($"\r  [{agentType}@{providerConfig.Provider}] {description} ... {toolCount} steps, {sw.Elapsed.TotalSeconds:F1}s");
            }

            sw.Stop();
            Console.WriteLine($"\r  [{agentType}@{providerConfig.Provider}] {description} - done ({toolCount} steps, {sw.Elapsed.TotalSeconds:F1}s)    ");

            return string.IsNullOrEmpty(finalText) ? "(subagent returned no text)" : finalText;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"\r  [{agentType}@{providerConfig.Provider}] {description} - error ({sw.Elapsed.TotalSeconds:F1}s)    ");
            return $"Error running subagent: {ex.Message}";
        }
    }

    /// <summary>
    /// Register the correct chat completion service on a kernel builder
    /// based on the provider configuration.
    /// </summary>
    private static void RegisterChatCompletion(IKernelBuilder builder, ModelProviderConfig config)
    {
        switch (config.Provider)
        {
            case ModelProvider.DeepSeek:
                builder.AddDeepSeekChatCompletion(config.ApiKey, config.ModelId);
                break;

            case ModelProvider.Zhipu:
                builder.AddZhipuChatCompletion(config.ApiKey, config.ModelId);
                break;

            case ModelProvider.OpenAI:
            default:
                if (!string.IsNullOrEmpty(config.BaseUrl))
                    builder.AddOpenAIChatCompletion(config.ModelId, new Uri(config.BaseUrl), config.ApiKey);
                else
                    builder.AddOpenAIChatCompletion(config.ModelId, config.ApiKey);
                break;
        }
    }

    private record AgentTypeConfig(string Description, string[] Tools, string Prompt);
}
