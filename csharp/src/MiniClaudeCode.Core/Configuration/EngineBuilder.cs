using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.Services;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Core.Agents;
using MiniClaudeCode.Core.AI;
using MiniClaudeCode.Core.Indexing;
using MiniClaudeCode.Core.Plugins;
using MiniClaudeCode.Core.Services;
using MiniClaudeCode.Core.Services.Providers;

namespace MiniClaudeCode.Core.Configuration;

/// <summary>
/// Builds the complete agent engine with all services, plugins, and configuration.
/// UI frontends call this to set up the core without knowing about Semantic Kernel internals.
/// </summary>
public class EngineBuilder
{
    private Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs = new();
    private ModelProvider _activeProvider;
    private Dictionary<string, ModelProvider> _agentProviderOverrides = new();
    private string _workDir = Directory.GetCurrentDirectory();
    private IOutputSink _output = new NullOutputSink();
    private IUserInteraction _interaction = null!;
    private IAgentProgressReporter _progressReporter = new NullAgentProgressReporter();

    public EngineBuilder WithProviders(Dictionary<ModelProvider, ModelProviderConfig> configs, ModelProvider active)
    {
        _providerConfigs = configs;
        _activeProvider = active;
        return this;
    }

    public EngineBuilder WithAgentProviderOverrides(Dictionary<string, ModelProvider> overrides)
    {
        _agentProviderOverrides = overrides;
        return this;
    }

    public EngineBuilder WithWorkDir(string workDir)
    {
        _workDir = workDir;
        return this;
    }

    public EngineBuilder WithOutputSink(IOutputSink output)
    {
        _output = output;
        return this;
    }

    public EngineBuilder WithUserInteraction(IUserInteraction interaction)
    {
        _interaction = interaction;
        return this;
    }

    public EngineBuilder WithProgressReporter(IAgentProgressReporter reporter)
    {
        _progressReporter = reporter;
        return this;
    }

    /// <summary>
    /// Build the complete engine context.
    /// </summary>
    public EngineContext Build()
    {
        if (_interaction == null)
            throw new InvalidOperationException("IUserInteraction must be provided.");

        var activeConfig = _providerConfigs[_activeProvider];

        // Services
        var todoManager = new TodoManager();
        var skillLoader = new SkillLoader(Path.Combine(_workDir, "skills"));
        var rulesLoader = new RulesLoader(_workDir);
        var agentRegistry = new AgentRegistry();

        // Agent system
        var subAgentRunner = new SubAgentRunner(
            _providerConfigs, _activeProvider, _agentProviderOverrides,
            _workDir, agentRegistry, _output, _progressReporter);
        var parallelExecutor = new ParallelAgentExecutor(subAgentRunner);
        var agentTeam = new AgentTeam(subAgentRunner, parallelExecutor, _output);

        // Indexing services
        var codebaseIndex = new CodebaseIndexService();
        var symbolIndex = new SymbolIndexService(codebaseIndex);

        // AI services
        var completionService = new InlineCompletionService(
            _providerConfigs, _activeProvider);
        var editService = new InlineEditService(
            _providerConfigs, _activeProvider);
        var composerService = new ComposerService(
            _providerConfigs, _activeProvider, _workDir, _output, agentRegistry);
        var contextBuilder = new ContextBuilder(_workDir);

        // Build kernel with all plugins
        var builder = Kernel.CreateBuilder();
        builder.AddProviderChatCompletion(activeConfig);

        builder.Plugins.AddFromObject(new CodingPlugin(_workDir, _output), "CodingPlugin");
        builder.Plugins.AddFromObject(new FileSearchPlugin(_workDir), "FileSearchPlugin");
        builder.Plugins.AddFromObject(new WebPlugin(_output), "WebPlugin");
        builder.Plugins.AddFromObject(new TodoPlugin(todoManager, _output), "TodoPlugin");
        builder.Plugins.AddFromObject(new TaskPlugin(subAgentRunner, _output), "TaskPlugin");
        builder.Plugins.AddFromObject(new SkillPlugin(skillLoader, _output), "SkillPlugin");
        builder.Plugins.AddFromObject(new RulesPlugin(rulesLoader, _output), "RulesPlugin");
        builder.Plugins.AddFromObject(new InteractionPlugin(_interaction), "InteractionPlugin");

        var kernel = builder.Build();

        // Build system prompt
        var systemPrompt = BuildSystemPrompt(_workDir, skillLoader, rulesLoader);

        // Create main agent
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

        return new EngineContext
        {
            Kernel = kernel,
            Agent = agent,
            Thread = new ChatHistoryAgentThread(),
            WorkDir = _workDir,
            ActiveConfig = activeConfig,
            ProviderConfigs = _providerConfigs,
            AgentProviderOverrides = _agentProviderOverrides,
            SkillLoader = skillLoader,
            RulesLoader = rulesLoader,
            TodoManager = todoManager,
            AgentRegistry = agentRegistry,
            SubAgentRunner = subAgentRunner,
            ParallelExecutor = parallelExecutor,
            AgentTeam = agentTeam,
            CodebaseIndex = codebaseIndex,
            SymbolIndex = symbolIndex,
            CompletionService = completionService,
            EditService = editService,
            ComposerService = composerService,
            ContextBuilder = contextBuilder,
            Output = _output,
            ProgressReporter = _progressReporter
        };
    }

    private static string BuildSystemPrompt(string workDir, ISkillLoader skillLoader, IRulesLoader rulesLoader)
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
            - Supports: model tiers (fast/default), resumption (resume param), read-only mode, attachments

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

/// <summary>
/// Holds all services and components built by the EngineBuilder.
/// Passed to UI frontends.
/// </summary>
public class EngineContext
{
    public required Kernel Kernel { get; set; }
    public required ChatCompletionAgent Agent { get; set; }
    public required ChatHistoryAgentThread Thread { get; set; }
    public required string WorkDir { get; set; }
    public required ModelProviderConfig ActiveConfig { get; set; }
    public required Dictionary<ModelProvider, ModelProviderConfig> ProviderConfigs { get; set; }
    public required Dictionary<string, ModelProvider> AgentProviderOverrides { get; set; }
    public required ISkillLoader SkillLoader { get; init; }
    public required IRulesLoader RulesLoader { get; init; }
    public required ITodoManager TodoManager { get; init; }
    public required AgentRegistry AgentRegistry { get; init; }
    public required SubAgentRunner SubAgentRunner { get; init; }
    public required ParallelAgentExecutor ParallelExecutor { get; init; }
    public required AgentTeam AgentTeam { get; init; }
    public required CodebaseIndexService CodebaseIndex { get; init; }
    public required SymbolIndexService SymbolIndex { get; init; }
    public required InlineCompletionService CompletionService { get; init; }
    public required InlineEditService EditService { get; init; }
    public required ComposerService ComposerService { get; init; }
    public required ContextBuilder ContextBuilder { get; init; }
    public required IOutputSink Output { get; init; }
    public required IAgentProgressReporter ProgressReporter { get; init; }

    public int TurnCount { get; set; }
    public int TotalToolCalls { get; set; }
    public bool ExitRequested { get; set; }

    public void ResetThread()
    {
        Thread = new ChatHistoryAgentThread();
        TurnCount = 0;
        TotalToolCalls = 0;
    }
}

/// <summary>
/// No-op output sink for use when no UI is attached (e.g., headless mode).
/// </summary>
internal class NullOutputSink : IOutputSink
{
    public void WriteAssistant(string content) { }
    public void WriteSystem(string message) { }
    public void WriteError(string message) { }
    public void WriteWarning(string message) { }
    public void WriteDebug(string message) { }
    public void WriteLine(string text = "") { }
    public void Clear() { }
    public void StreamStart(string messageId) { }
    public void StreamAppend(string messageId, string chunk) { }
    public void StreamEnd(string messageId) { }
}

/// <summary>
/// No-op progress reporter for when no UI is attached.
/// </summary>
internal class NullAgentProgressReporter : IAgentProgressReporter
{
    public void OnAgentStarted(AgentInstanceInfo agent) { }
    public void OnAgentProgress(AgentProgressEvent progress) { }
    public void OnAgentCompleted(AgentResult result) { }
    public void OnAgentFailed(AgentInstanceInfo agent, string error) { }
}
