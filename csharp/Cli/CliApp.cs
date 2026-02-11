using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Cli.Commands;
using MiniClaudeCode.Plugins;
using MiniClaudeCode.Services;
using MiniClaudeCode.Services.Providers;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Main application lifecycle.
///
/// 1. Load configuration (env, providers)
/// 2. Build kernel with plugins and tool visualizer
/// 3. Create agent
/// 4. Run REPL or print mode
/// </summary>
public class CliApp
{
    public async Task RunAsync(CliArgs cliArgs)
    {
        // --- Version ---
        if (cliArgs.ShowVersion)
        {
            AnsiConsole.MarkupLine($"[bold]Mini Claude Code[/] v{CliArgs.Version}");
            return;
        }

        // --- Load .env ---
        LoadEnvFiles();

        // --- Load providers ---
        var providerConfigs = ModelProviderConfig.LoadAll();
        if (providerConfigs.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No model provider configured.[/]");
            AnsiConsole.MarkupLine("[dim]Set at least one API key in .env: OPENAI_API_KEY, DEEPSEEK_API_KEY, or ZHIPU_API_KEY[/]");
            return;
        }

        // --- Resolve active provider ---
        ModelProvider activeProvider;
        if (!string.IsNullOrEmpty(cliArgs.ProviderOverride))
        {
            var parsed = ModelProviderConfig.ParseProviderName(cliArgs.ProviderOverride);
            if (parsed.HasValue && providerConfigs.ContainsKey(parsed.Value))
                activeProvider = parsed.Value;
            else
            {
                AnsiConsole.MarkupLine($"[red]Unknown provider: {Markup.Escape(cliArgs.ProviderOverride)}[/]");
                return;
            }
        }
        else
        {
            activeProvider = ModelProviderConfig.ResolveActiveProvider(providerConfigs);
        }

        var activeConfig = providerConfigs[activeProvider];

        // Apply model override from CLI args
        if (!string.IsNullOrEmpty(cliArgs.ModelOverride))
            activeConfig.ModelId = cliArgs.ModelOverride;

        var agentProviderOverrides = ModelProviderConfig.LoadAgentProviderOverrides(providerConfigs);
        var workDir = Directory.GetCurrentDirectory();

        // --- Initialize services ---
        var todoManager = new TodoManager();
        var skillLoader = new SkillLoader(Path.Combine(workDir, "skills"));
        var rulesLoader = new RulesLoader(workDir);
        var subAgentRunner = new SubAgentRunner(providerConfigs, activeProvider, agentProviderOverrides, workDir);

        // --- Build kernel ---
        var builder = Kernel.CreateBuilder();
        RegisterChatCompletion(builder, activeConfig);

        // Register plugins
        builder.Plugins.AddFromObject(new CodingPlugin(workDir), "CodingPlugin");
        builder.Plugins.AddFromObject(new FileSearchPlugin(workDir), "FileSearchPlugin");
        builder.Plugins.AddFromObject(new WebPlugin(), "WebPlugin");
        builder.Plugins.AddFromObject(new TodoPlugin(todoManager), "TodoPlugin");
        builder.Plugins.AddFromObject(new TaskPlugin(subAgentRunner), "TaskPlugin");
        builder.Plugins.AddFromObject(new SkillPlugin(skillLoader), "SkillPlugin");
        builder.Plugins.AddFromObject(new RulesPlugin(rulesLoader), "RulesPlugin");
        builder.Plugins.AddFromObject(new InteractionPlugin(), "InteractionPlugin");

        var kernel = builder.Build();

        // --- Build system prompt ---
        var systemPrompt = BuildSystemPrompt(workDir, skillLoader, rulesLoader);

        // --- Create agent ---
        var agent = new ChatCompletionAgent
        {
            Name = "MiniClaudeCode",
            Instructions = systemPrompt,
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        // --- Build CLI context ---
        var ctx = new CliContext
        {
            Kernel = kernel,
            Agent = agent,
            Thread = new ChatHistoryAgentThread(),
            WorkDir = workDir,
            ActiveConfig = activeConfig,
            ProviderConfigs = providerConfigs,
            AgentProviderOverrides = agentProviderOverrides,
            SkillLoader = skillLoader,
            RulesLoader = rulesLoader,
            TodoManager = todoManager,
            SubAgentRunner = subAgentRunner,
        };

        // --- Register tool call visualizer ---
        kernel.AutoFunctionInvocationFilters.Add(new ToolCallVisualizer(ctx));

        // --- Run ---
        if (cliArgs.Mode == CliMode.Print && !string.IsNullOrEmpty(cliArgs.Prompt))
        {
            await RunPrintMode(ctx, cliArgs.Prompt);
        }
        else
        {
            var repl = new ReplLoop(ctx);
            await repl.RunAsync(cliArgs.Prompt);
        }
    }

    /// <summary>
    /// Print mode: answer a single prompt and exit.
    /// </summary>
    private static async Task RunPrintMode(CliContext ctx, string prompt)
    {
        var message = new ChatMessageContent(AuthorRole.User, prompt);
        await foreach (var response in ctx.Agent.InvokeAsync(message, ctx.Thread))
        {
            if (!string.IsNullOrEmpty(response.Message.Content))
                OutputRenderer.RenderAssistantMessage(response.Message.Content);
        }
    }

    /// <summary>
    /// Load .env files from multiple locations.
    /// </summary>
    private static void LoadEnvFiles()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath))
            DotNetEnv.Env.Load(envPath);

        var projectEnv = Path.Combine(Directory.GetCurrentDirectory(), "csharp", ".env");
        if (File.Exists(projectEnv))
            DotNetEnv.Env.Load(projectEnv);

        var cwdEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(cwdEnv))
            DotNetEnv.Env.Load(cwdEnv);
    }

    /// <summary>
    /// Register the correct chat completion service based on provider.
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

    /// <summary>
    /// Build the system prompt for the agent.
    /// </summary>
    private static string BuildSystemPrompt(string workDir, SkillLoader skillLoader, RulesLoader rulesLoader)
    {
        var skillDescriptions = skillLoader.GetDescriptions();
        var agentDescriptions = SubAgentRunner.GetAgentDescriptions();
        var alwaysApplyRules = rulesLoader.GetAlwaysApplyRulesContent();
        var agentDecidedRules = rulesLoader.GetAgentDecidedRulesDescriptions();

        return $"""
            You are a coding agent at {workDir}.

            Loop: plan -> act with tools -> report.

            **Tools available:**
            - File ops: bash, read_file, write_file, edit_file
            - Search: grep (content), glob (file names), list_directory (tree)
            - Web: web_search (internet search), web_fetch (read URL)
            - Planning: TodoWrite (track multi-step tasks)
            - Interaction: ask_question (clarify with user)

            **Skills available** (invoke with Skill tool when task matches):
            {skillDescriptions}

            **Subagents available** (invoke with Task tool for focused subtasks):
            {agentDescriptions}

            **Project rules on-demand** (invoke with fetch_rule when relevant):
            {agentDecidedRules}

            Guidelines:
            - Use Skill tool IMMEDIATELY when a task matches a skill description
            - Use Task tool for subtasks needing focused exploration or implementation
            - Use TodoWrite to track multi-step work
            - Use ask_question when requirements are unclear or ambiguous
            - Use glob/list_directory to discover files, grep to search content
            - Use web_search/web_fetch for up-to-date docs or external information
            - Prefer tools over prose. Act, don't just explain.
            - Never invent file paths. Use glob or list_directory first if unsure.
            - Make minimal changes. Don't over-engineer.
            - After finishing, summarize what changed.

            {alwaysApplyRules}
            """;
    }
}
