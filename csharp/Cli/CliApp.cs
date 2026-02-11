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

        // --- Load .env from all possible locations ---
        LoadEnvFiles();

        // --- Load providers ---
        var providerConfigs = ModelProviderConfig.LoadAll();
        if (providerConfigs.Count == 0)
        {
            // Interactive setup wizard when no providers configured
            if (cliArgs.Mode == CliMode.Interactive)
            {
                providerConfigs = await RunSetupWizardAsync();
                if (providerConfigs.Count == 0)
                    return; // User cancelled
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error: No model provider configured.[/]");
                AnsiConsole.MarkupLine("[dim]Set at least one API key in .env: OPENAI_API_KEY, DEEPSEEK_API_KEY, or ZHIPU_API_KEY[/]");
                return;
            }
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

        // --- Resolve working directory ---
        var workDir = ResolveWorkingDirectory(cliArgs);

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
    /// Interactive setup wizard for first-time users (no .env found).
    /// Guides through provider selection and API key entry.
    /// </summary>
    private static async Task<Dictionary<ModelProvider, ModelProviderConfig>> RunSetupWizardAsync()
    {
        AnsiConsole.Write(new Panel(
            Align.Center(new Markup(
                "[bold cyan]Mini Claude Code[/]  [dim]First-Time Setup[/]\n" +
                "[dim]── C# Semantic Kernel Edition ──[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 0, 2, 0),
        });
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]No API key found.[/] Let's set one up.\n");

        var provider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Which model provider do you want to use?[/]")
                .AddChoices("DeepSeek", "Zhipu (GLM)", "OpenAI")
                .HighlightStyle(new Style(Color.Cyan1)));

        var (envKey, defaultModel) = provider switch
        {
            "DeepSeek" => ("DEEPSEEK_API_KEY", "deepseek-chat"),
            "Zhipu (GLM)" => ("ZHIPU_API_KEY", "glm-4-plus"),
            _ => ("OPENAI_API_KEY", "gpt-4o"),
        };

        AnsiConsole.WriteLine();
        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>($"[cyan]Enter your API key ({envKey}):[/]")
                .Secret());

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AnsiConsole.MarkupLine("[red]No API key provided. Exiting.[/]");
            return [];
        }

        // Save to .env next to the exe
        var envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
        var envContent = $"ACTIVE_PROVIDER={provider switch { "DeepSeek" => "deepseek", "Zhipu (GLM)" => "zhipu", _ => "openai" }}\n{envKey}={apiKey}\n";

        try
        {
            await File.WriteAllTextAsync(envFilePath, envContent);
            AnsiConsole.MarkupLine($"\n[green]Saved to {Markup.Escape(envFilePath)}[/]");
        }
        catch
        {
            // If can't write to exe dir, try current dir
            envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            try
            {
                await File.WriteAllTextAsync(envFilePath, envContent);
                AnsiConsole.MarkupLine($"\n[green]Saved to {Markup.Escape(envFilePath)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[yellow]Could not save .env: {Markup.Escape(ex.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Setting environment variable for this session only.[/]");
            }
        }

        // Set the env var for this session
        Environment.SetEnvironmentVariable(envKey, apiKey);

        AnsiConsole.WriteLine();

        // Reload providers
        return ModelProviderConfig.LoadAll();
    }

    /// <summary>
    /// Resolve the working directory.
    /// When double-clicking the exe, the CWD might be wrong (e.g. system32).
    /// In that case, use the exe's directory or prompt the user.
    /// </summary>
    private static string ResolveWorkingDirectory(CliArgs cliArgs)
    {
        var cwd = Directory.GetCurrentDirectory();

        // If add-dir was specified, use the first one
        if (cliArgs.AddDirs.Count > 0)
        {
            var dir = Path.GetFullPath(cliArgs.AddDirs[0]);
            if (Directory.Exists(dir))
            {
                Directory.SetCurrentDirectory(dir);
                return dir;
            }
        }

        // Check if CWD looks like a system directory (double-click scenario)
        var isSysDir = cwd.Contains("System32", StringComparison.OrdinalIgnoreCase)
                    || cwd.Contains("system32", StringComparison.OrdinalIgnoreCase)
                    || cwd.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase);

        if (isSysDir)
        {
            // Use the exe's directory instead
            var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            AnsiConsole.MarkupLine($"[yellow]Working directory is a system folder. Using exe location:[/]");
            AnsiConsole.MarkupLine($"  [blue]{Markup.Escape(exeDir)}[/]\n");
            Directory.SetCurrentDirectory(exeDir);
            return exeDir;
        }

        return cwd;
    }

    /// <summary>
    /// Load .env files from multiple locations.
    /// Search order: exe dir, walk up from exe dir, cwd/csharp, cwd, user home.
    /// This ensures .env is found whether running via `dotnet run` or double-clicking the exe.
    /// </summary>
    private static void LoadEnvFiles()
    {
        var loaded = false;

        // 1. Next to the executable (bin/Debug/net10.0/.env — copied by build)
        var exeEnv = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(exeEnv))
        {
            DotNetEnv.Env.Load(exeEnv);
            loaded = true;
        }

        // 2. Walk up from exe directory to find .env (handles bin/Debug/net10.0 -> project root)
        if (!loaded)
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 5 && dir != null; i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (dir != null)
                {
                    var candidate = Path.Combine(dir, ".env");
                    if (File.Exists(candidate))
                    {
                        DotNetEnv.Env.Load(candidate);
                        loaded = true;
                        break;
                    }
                }
            }
        }

        // 3. cwd/csharp/.env (dev layout: running from repo root)
        var projectEnv = Path.Combine(Directory.GetCurrentDirectory(), "csharp", ".env");
        if (File.Exists(projectEnv))
            DotNetEnv.Env.Load(projectEnv);

        // 4. Current working directory
        var cwdEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(cwdEnv))
            DotNetEnv.Env.Load(cwdEnv);

        // 5. User home directory (~/.miniclaudecode/.env)
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeEnv = Path.Combine(homeDir, ".miniclaudecode", ".env");
        if (File.Exists(homeEnv))
            DotNetEnv.Env.Load(homeEnv);
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
