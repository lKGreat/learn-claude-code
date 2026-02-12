using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Avalonia.Adapters;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Root ViewModel that orchestrates all panels and the engine lifecycle.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public ChatViewModel Chat { get; } = new();
    public AgentPanelViewModel AgentPanel { get; } = new();
    public TodoPanelViewModel TodoPanel { get; } = new();
    public ToolCallPanelViewModel ToolCallPanel { get; } = new();
    public SetupWizardViewModel SetupWizard { get; } = new();
    public QuestionDialogViewModel QuestionDialog { get; } = new();

    [ObservableProperty]
    private string _windowTitle = "MiniClaudeCode v0.3.0 - Avalonia";

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _providerDisplay = "";

    [ObservableProperty]
    private int _turnCount;

    [ObservableProperty]
    private int _toolCallCount;

    private EngineContext? _engine;
    private bool _initialized;

    public MainWindowViewModel()
    {
        Chat.MessageSubmitted += OnMessageSubmitted;
    }

    /// <summary>
    /// Initialize the engine asynchronously after the window is shown.
    /// </summary>
    public async void InitializeAsync(string[] args)
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            await InitializeEngineAsync();
        }
        catch (Exception ex)
        {
            DispatcherService.Post(() =>
            {
                Chat.AddErrorMessage($"Initialization failed: {ex.Message}");
                StatusText = "Initialization failed";
            });
        }
    }

    private async Task InitializeEngineAsync()
    {
        // Load .env files
        LoadEnvFiles();

        // Load provider configs
        var providerConfigs = ModelProviderConfig.LoadAll();

        if (providerConfigs.Count == 0)
        {
            // Show setup wizard
            DispatcherService.Post(() => SetupWizard.Show());
            var result = await SetupWizard.WaitForResultAsync();

            if (result == null)
            {
                DispatcherService.Post(() =>
                {
                    Chat.AddSystemMessage("Setup cancelled. Please configure a provider and restart.");
                    StatusText = "No provider configured";
                });
                return;
            }

            // Set environment variable based on chosen provider
            var envKey = result.Provider switch
            {
                "OpenAI" => "OPENAI_API_KEY",
                "DeepSeek" => "DEEPSEEK_API_KEY",
                "Zhipu" => "ZHIPU_API_KEY",
                _ => "OPENAI_API_KEY"
            };
            Environment.SetEnvironmentVariable(envKey, result.ApiKey);

            // Reload providers
            providerConfigs = ModelProviderConfig.LoadAll();
            if (providerConfigs.Count == 0)
            {
                DispatcherService.Post(() =>
                {
                    Chat.AddErrorMessage("Failed to configure provider. Please check your API key.");
                    StatusText = "Configuration error";
                });
                return;
            }
        }

        // Resolve active provider
        var activeProvider = ModelProviderConfig.ResolveActiveProvider(providerConfigs);
        var activeConfig = providerConfigs[activeProvider];
        var agentProviderOverrides = ModelProviderConfig.LoadAgentProviderOverrides(providerConfigs);
        var workDir = Directory.GetCurrentDirectory();

        // Create adapters wired to ViewModels
        var outputSink = new AvaloniaOutputSink(Chat);
        var userInteraction = new AvaloniaUserInteraction(QuestionDialog);
        var progressReporter = new AvaloniaProgressReporter(AgentPanel, Chat);
        var toolCallObserver = new AvaloniaToolCallObserver(ToolCallPanel, Chat);

        // Build engine
        var engine = new EngineBuilder()
            .WithProviders(providerConfigs, activeProvider)
            .WithAgentProviderOverrides(agentProviderOverrides)
            .WithWorkDir(workDir)
            .WithOutputSink(outputSink)
            .WithUserInteraction(userInteraction)
            .WithProgressReporter(progressReporter)
            .Build();

        // Register tool call filter
        var filter = new AvaloniaToolCallFilter(toolCallObserver, engine);
        engine.Kernel.AutoFunctionInvocationFilters.Add(filter);

        _engine = engine;

        DispatcherService.Post(() =>
        {
            ProviderDisplay = activeConfig.DisplayName;
            WindowTitle = $"MiniClaudeCode v0.3.0 - {activeConfig.DisplayName}";
            StatusText = $"{activeConfig.DisplayName} | {workDir}";
            Chat.AddSystemMessage(
                $"MiniClaudeCode v0.3.0 | {activeConfig.DisplayName}\n" +
                $"Workspace: {workDir}\n" +
                "Type your message or use /help for commands. (Enter = send, Shift+Enter = newline)");
        });
    }

    private void OnMessageSubmitted(string input)
    {
        if (IsProcessing)
        {
            Chat.AddSystemMessage("Please wait - still processing...");
            return;
        }

        // Slash commands
        if (input.StartsWith('/'))
        {
            HandleSlashCommand(input);
            return;
        }

        // Agent message
        IsProcessing = true;
        Chat.IsProcessing = true;
        Chat.AddUserMessage(input);

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAgentMessageAsync(input);
            }
            finally
            {
                DispatcherService.Post(() =>
                {
                    IsProcessing = false;
                    Chat.IsProcessing = false;
                });
            }
        });
    }

    private void HandleSlashCommand(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "/help" or "/h" or "/?":
                Chat.AddSystemMessage(
                    "Available commands:\n" +
                    "  /help       - Show this help\n" +
                    "  /new        - Start new conversation\n" +
                    "  /clear      - Clear chat display\n" +
                    "  /status     - Show session status\n" +
                    "  /model      - Show current model info\n" +
                    "  /compact    - Summarize conversation\n" +
                    "  /exit       - Exit application\n" +
                    "\n" +
                    "Keyboard:\n" +
                    "  Enter       - Send message\n" +
                    "  Shift+Enter - New line\n" +
                    "  Ctrl+N      - New conversation\n" +
                    "  Ctrl+L      - Clear chat");
                break;

            case "/new" or "/reset":
                if (_engine != null)
                {
                    _engine.ResetThread();
                    TurnCount = 0;
                    ToolCallCount = 0;
                    Chat.ClearMessages();
                    Chat.AddSystemMessage("Started a new conversation.");
                    UpdateTitle();
                }
                break;

            case "/clear" or "/cls":
                Chat.ClearMessages();
                break;

            case "/status" or "/stat":
                if (_engine != null)
                {
                    Chat.AddSystemMessage(
                        $"Provider: {_engine.ActiveConfig.DisplayName}\n" +
                        $"Model: {_engine.ActiveConfig.ModelId}\n" +
                        $"Workspace: {_engine.WorkDir}\n" +
                        $"Turns: {TurnCount}\n" +
                        $"Tool Calls: {ToolCallCount}\n" +
                        $"Agents: {_engine.AgentRegistry.Count} registered");
                }
                break;

            case "/model":
                if (_engine != null)
                {
                    Chat.AddSystemMessage(
                        $"Provider: {_engine.ActiveConfig.Provider}\n" +
                        $"Model: {_engine.ActiveConfig.ModelId}\n" +
                        $"Display: {_engine.ActiveConfig.DisplayName}");
                }
                break;

            case "/compact":
                Chat.AddSystemMessage("Compacting conversation...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var summary = await CompactConversationAsync();
                        DispatcherService.Post(() =>
                        {
                            Chat.ClearMessages();
                            Chat.AddSystemMessage($"Conversation compacted. Summary:\n{summary}");
                        });
                    }
                    catch (Exception ex)
                    {
                        DispatcherService.Post(() =>
                            Chat.AddErrorMessage($"Compact failed: {ex.Message}"));
                    }
                });
                break;

            case "/exit" or "/quit" or "/q":
                // Close the application
                if (global::Avalonia.Application.Current?.ApplicationLifetime
                    is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                break;

            default:
                Chat.AddSystemMessage($"Unknown command: {command}. Type /help for available commands.");
                break;
        }
    }

    private async Task ProcessAgentMessageAsync(string input)
    {
        if (_engine == null) return;

        _engine.TurnCount++;
        DispatcherService.Post(() =>
        {
            TurnCount = _engine.TurnCount;
            ToolCallCount = _engine.TotalToolCalls;
            UpdateTitle();
        });

        try
        {
            var message = new ChatMessageContent(AuthorRole.User, input);
            string lastContent = "";

            await foreach (var response in _engine.Agent.InvokeAsync(message, _engine.Thread))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                    lastContent = response.Message.Content;
            }

            if (!string.IsNullOrEmpty(lastContent))
            {
                DispatcherService.Post(() =>
                {
                    Chat.AddAssistantMessage(lastContent);
                    ToolCallCount = _engine.TotalToolCalls;
                    UpdateTitle();

                    // Sync todos from engine
                    SyncTodos();
                });
            }
        }
        catch (OperationCanceledException)
        {
            DispatcherService.Post(() => Chat.AddSystemMessage("Operation cancelled."));
        }
        catch (Exception ex)
        {
            DispatcherService.Post(() => Chat.AddErrorMessage($"Error: {ex.Message}"));
        }
    }

    private async Task<string> CompactConversationAsync()
    {
        if (_engine == null) return "No engine available.";

        var compactMessage = new ChatMessageContent(AuthorRole.User,
            "Please summarize our conversation so far in a concise paragraph. " +
            "Include key decisions, code changes made, and any pending tasks.");

        string summary = "";
        await foreach (var response in _engine.Agent.InvokeAsync(compactMessage, _engine.Thread))
        {
            if (!string.IsNullOrEmpty(response.Message.Content))
                summary = response.Message.Content;
        }

        _engine.ResetThread();
        return summary;
    }

    private void SyncTodos()
    {
        if (_engine?.TodoManager == null) return;

        var rendered = _engine.TodoManager.Render();
        TodoPanel.UpdateFromRendered(rendered);
    }

    private void UpdateTitle()
    {
        WindowTitle = _engine != null
            ? $"MiniClaudeCode - {_engine.ActiveConfig.DisplayName} | Turn:{TurnCount} Tools:{ToolCallCount}"
            : "MiniClaudeCode v0.3.0 - Avalonia";
    }

    [RelayCommand]
    private void NewConversation()
    {
        HandleSlashCommand("/new");
    }

    [RelayCommand]
    private void ClearChat()
    {
        Chat.ClearMessages();
    }

    [RelayCommand]
    private void ShowStatus()
    {
        HandleSlashCommand("/status");
    }

    [RelayCommand]
    private void ExitApp()
    {
        HandleSlashCommand("/exit");
    }

    private static void LoadEnvFiles()
    {
        var loaded = false;

        var exeEnv = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(exeEnv))
        {
            DotNetEnv.Env.Load(exeEnv);
            loaded = true;
        }

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

        var projectEnv = Path.Combine(Directory.GetCurrentDirectory(), "csharp", ".env");
        if (File.Exists(projectEnv))
            DotNetEnv.Env.Load(projectEnv);

        var cwdEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(cwdEnv))
            DotNetEnv.Env.Load(cwdEnv);

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeEnv = Path.Combine(homeDir, ".miniclaudecode", ".env");
        if (File.Exists(homeEnv))
            DotNetEnv.Env.Load(homeEnv);
    }
}
