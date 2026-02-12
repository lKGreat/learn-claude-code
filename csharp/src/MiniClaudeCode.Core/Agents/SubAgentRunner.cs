using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Core.Configuration;
using MiniClaudeCode.Core.Plugins;
using MiniClaudeCode.Core.Services.Providers;

namespace MiniClaudeCode.Core.Agents;

/// <summary>
/// Creates and runs isolated subagents for focused subtasks.
/// Enhanced with: resumption, model tiers, read-only mode, attachments, progress streaming.
/// </summary>
public class SubAgentRunner
{
    private readonly Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs;
    private readonly ModelProvider _defaultProvider;
    private readonly Dictionary<string, ModelProvider> _agentProviderOverrides;
    private readonly string _workDir;
    private readonly AgentRegistry _registry;
    private readonly IOutputSink _output;
    private readonly IAgentProgressReporter _progressReporter;

    private record AgentTypeConfig(string Description, string[] Tools, string Prompt, bool IsReadOnly);

    /// <summary>
    /// Agent type configurations.
    /// Now includes generalPurpose type aligned with Claude Code, plus completion agent for fast inline completions.
    /// </summary>
    private static readonly Dictionary<string, AgentTypeConfig> AgentTypes = new()
    {
        ["generalPurpose"] = new(
            Description: "General-purpose agent for researching complex questions and multi-step tasks",
            Tools: ["*"],
            Prompt: "You are a general-purpose agent. Research the question thoroughly, search code, and provide a comprehensive answer.",
            IsReadOnly: false
        ),
        ["explore"] = new(
            Description: "Read-only agent for exploring code, finding files, searching",
            Tools: ["bash", "read_file", "grep", "glob", "list_directory"],
            Prompt: "You are an exploration agent. Search and analyze, but never modify files. Return a concise summary.",
            IsReadOnly: true
        ),
        ["code"] = new(
            Description: "Full agent for implementing features and fixing bugs",
            Tools: ["*"],
            Prompt: "You are a coding agent. Implement the requested changes efficiently.",
            IsReadOnly: false
        ),
        ["plan"] = new(
            Description: "Planning agent for designing implementation strategies",
            Tools: ["bash", "read_file", "grep", "glob", "list_directory"],
            Prompt: "You are a planning agent. Analyze the codebase and output a numbered implementation plan. Do NOT make changes.",
            IsReadOnly: true
        ),
        ["completion"] = new(
            Description: "Lightweight fast agent for inline code completions (no tools, low latency)",
            Tools: [],
            Prompt: "You are a code completion engine. Generate completions quickly and accurately. Return ONLY the completion text, no explanations.",
            IsReadOnly: true
        ),
    };

    public SubAgentRunner(
        Dictionary<ModelProvider, ModelProviderConfig> providerConfigs,
        ModelProvider defaultProvider,
        Dictionary<string, ModelProvider> agentProviderOverrides,
        string workDir,
        AgentRegistry registry,
        IOutputSink output,
        IAgentProgressReporter progressReporter)
    {
        _providerConfigs = providerConfigs;
        _defaultProvider = defaultProvider;
        _agentProviderOverrides = agentProviderOverrides;
        _workDir = workDir;
        _registry = registry;
        _output = output;
        _progressReporter = progressReporter;
    }

    private ModelProviderConfig GetProviderForAgent(string agentType, string? modelTier)
    {
        // Model tier override (fast = use cheapest available)
        if (modelTier == "fast")
        {
            // Prefer DeepSeek for "fast" tier if available
            if (_providerConfigs.TryGetValue(ModelProvider.DeepSeek, out var dsConfig))
                return dsConfig;
        }

        // Per-agent-type override
        if (_agentProviderOverrides.TryGetValue(agentType, out var overrideProvider)
            && _providerConfigs.TryGetValue(overrideProvider, out var overrideConfig))
            return overrideConfig;

        return _providerConfigs[_defaultProvider];
    }

    public static string GetAgentDescriptions()
    {
        return string.Join("\n", AgentTypes.Select(kv =>
            $"- {kv.Key}: {kv.Value.Description}"));
    }

    public static IEnumerable<string> GetAgentTypeNames() => AgentTypes.Keys;

    /// <summary>
    /// Run an agent task. Supports new agent creation or resumption of existing agent.
    /// </summary>
    public async Task<AgentResult> RunAsync(AgentTask task, CancellationToken ct = default)
    {
        // Try to resume existing agent
        if (!string.IsNullOrEmpty(task.ResumeAgentId))
        {
            return await ResumeAgentAsync(task, ct);
        }

        return await RunNewAgentAsync(task, ct);
    }

    /// <summary>
    /// Resume an existing agent by ID.
    /// </summary>
    private async Task<AgentResult> ResumeAgentAsync(AgentTask task, CancellationToken ct)
    {
        if (!_registry.TryGet(task.ResumeAgentId!, out var info, out var thread, out var agent))
        {
            return new AgentResult
            {
                AgentId = task.ResumeAgentId!,
                Output = $"Error: Agent '{task.ResumeAgentId}' not found or expired.",
                IsError = true
            };
        }

        _registry.UpdateStatus(info!.Id, AgentStatus.Running);
        _progressReporter.OnAgentStarted(info);

        var sw = Stopwatch.StartNew();
        int toolCount = info.ToolCallCount;

        try
        {
            var userMessage = new ChatMessageContent(AuthorRole.User, task.Prompt);
            string finalText = "";

            await foreach (var response in agent!.InvokeAsync(userMessage, thread!).WithCancellation(ct))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                    finalText = response.Message.Content;

                toolCount++;
                _registry.IncrementToolCalls(info.Id);

                _progressReporter.OnAgentProgress(new AgentProgressEvent
                {
                    AgentId = info.Id,
                    Step = toolCount,
                    Elapsed = sw.Elapsed,
                    Message = $"{task.Description} ... {toolCount} steps, {sw.Elapsed.TotalSeconds:F1}s",
                    Description = task.Description,
                    AgentType = task.AgentType
                });
            }

            sw.Stop();
            _registry.UpdateStatus(info.Id, AgentStatus.Suspended); // Suspened for potential re-resume

            var result = new AgentResult
            {
                AgentId = info.Id,
                Output = string.IsNullOrEmpty(finalText) ? "(agent returned no text)" : finalText,
                ToolCallCount = toolCount,
                Elapsed = sw.Elapsed
            };

            _progressReporter.OnAgentCompleted(result);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _registry.UpdateStatus(info.Id, AgentStatus.Failed);

            var result = new AgentResult
            {
                AgentId = info.Id,
                Output = $"Error resuming agent: {ex.Message}",
                IsError = true,
                ErrorMessage = ex.Message,
                Elapsed = sw.Elapsed
            };

            _progressReporter.OnAgentFailed(info, ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Create and run a new agent.
    /// </summary>
    private async Task<AgentResult> RunNewAgentAsync(AgentTask task, CancellationToken ct)
    {
        if (!AgentTypes.TryGetValue(task.AgentType, out var config))
        {
            return new AgentResult
            {
                AgentId = "",
                Output = $"Error: Unknown agent type '{task.AgentType}'. Available: {string.Join(", ", AgentTypes.Keys)}",
                IsError = true
            };
        }

        var providerConfig = GetProviderForAgent(task.AgentType, task.ModelTier);
        var agentId = Guid.NewGuid().ToString("N")[..12];

        var agentInfo = new AgentInstanceInfo
        {
            Id = agentId,
            AgentType = task.AgentType,
            Description = task.Description,
            Status = AgentStatus.Running,
            ModelTier = task.ModelTier,
            ReadOnly = task.ReadOnly || config.IsReadOnly
        };

        _progressReporter.OnAgentStarted(agentInfo);

        var sw = Stopwatch.StartNew();
        int toolCount = 0;

        try
        {
            // Build isolated kernel with filtered plugins
            var builder = Kernel.CreateBuilder();
            builder.AddProviderChatCompletion(providerConfig);

            var codingPlugin = new CodingPlugin(_workDir, _output);
            var fileSearchPlugin = new FileSearchPlugin(_workDir);

            var codingKernelPlugin = KernelPluginFactory.CreateFromObject(codingPlugin, "CodingPlugin");
            var fileSearchKernelPlugin = KernelPluginFactory.CreateFromObject(fileSearchPlugin, "FileSearchPlugin");

            var isReadOnly = task.ReadOnly || config.IsReadOnly;

            // Filter tools based on agent type and read-only mode
            // Special case: "completion" agent has NO tools for maximum speed
            if (task.AgentType != "completion")
            {
                if (config.Tools.Contains("*") && !isReadOnly)
                {
                    builder.Plugins.Add(codingKernelPlugin);
                    builder.Plugins.Add(fileSearchKernelPlugin);
                }
                else if (config.Tools.Length > 0)
                {
                    var allFunctions = codingKernelPlugin.Concat(fileSearchKernelPlugin).ToList();

                    string[] allowedTools;
                    if (isReadOnly)
                    {
                        // Read-only: only allow read tools
                        allowedTools = ["bash", "read_file", "grep", "glob", "list_directory"];
                    }
                    else
                    {
                        allowedTools = config.Tools;
                    }

                    var filteredFunctions = allFunctions
                        .Where(f => allowedTools.Contains(f.Name))
                        .ToList();

                    if (filteredFunctions.Count > 0)
                    {
                        var filteredPlugin = KernelPluginFactory.CreateFromFunctions("Tools", filteredFunctions);
                        builder.Plugins.Add(filteredPlugin);
                    }
                }
            }
            // else: completion agent gets no plugins

            var kernel = builder.Build();

            // Build prompt with optional attachment context
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"You are a {task.AgentType} subagent at {_workDir}.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(config.Prompt);

            if (isReadOnly)
                promptBuilder.AppendLine("\nIMPORTANT: You are in READ-ONLY mode. Do NOT modify any files.");

            promptBuilder.AppendLine("\nComplete the task and return a clear, concise summary.");

            // Prepare user message with attachments
            var userPrompt = new StringBuilder(task.Prompt);

            if (task.Attachments is { Count: > 0 })
            {
                userPrompt.AppendLine("\n\n--- Attached Files ---");
                foreach (var attachment in task.Attachments)
                {
                    try
                    {
                        var content = File.ReadAllText(Path.Combine(_workDir, attachment));
                        userPrompt.AppendLine($"\n--- {attachment} ---");
                        userPrompt.AppendLine(content.Length > 10000 ? content[..10000] + "\n... (truncated)" : content);
                    }
                    catch (Exception ex)
                    {
                        userPrompt.AppendLine($"\n--- {attachment} --- (error: {ex.Message})");
                    }
                }
            }

            // Create isolated agent
            // Optimize settings for completion agent: no tools, low temp, low max tokens
            var executionSettings = task.AgentType == "completion"
                ? new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.0,
                    MaxTokens = 200,
                    TopP = 0.95
                }
                : new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                };

            var agent = new ChatCompletionAgent
            {
                Name = $"SubAgent_{task.AgentType}_{agentId}",
                Instructions = promptBuilder.ToString(),
                Kernel = kernel,
                Arguments = new KernelArguments(executionSettings)
            };

            var thread = new ChatHistoryAgentThread();

            // Register with registry for potential resumption
            _registry.Register(agentInfo, thread, agent);

            var userMessage = new ChatMessageContent(AuthorRole.User, userPrompt.ToString());
            string finalText = "";

            await foreach (var response in agent.InvokeAsync(userMessage, thread).WithCancellation(ct))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                    finalText = response.Message.Content;

                toolCount++;
                _registry.IncrementToolCalls(agentId);

                _progressReporter.OnAgentProgress(new AgentProgressEvent
                {
                    AgentId = agentId,
                    Step = toolCount,
                    Elapsed = sw.Elapsed,
                    Message = $"{task.Description} ... {toolCount} steps, {sw.Elapsed.TotalSeconds:F1}s",
                    Description = task.Description,
                    AgentType = task.AgentType
                });
            }

            sw.Stop();
            _registry.UpdateStatus(agentId, AgentStatus.Suspended);

            var result = new AgentResult
            {
                AgentId = agentId,
                Output = string.IsNullOrEmpty(finalText) ? "(subagent returned no text)" : finalText,
                ToolCallCount = toolCount,
                Elapsed = sw.Elapsed
            };

            _progressReporter.OnAgentCompleted(result);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _registry.UpdateStatus(agentId, AgentStatus.Failed);
            _progressReporter.OnAgentFailed(agentInfo, ex.Message);

            return new AgentResult
            {
                AgentId = agentId,
                Output = $"Error running subagent: {ex.Message}",
                IsError = true,
                ErrorMessage = ex.Message,
                ToolCallCount = toolCount,
                Elapsed = sw.Elapsed
            };
        }
    }
}
