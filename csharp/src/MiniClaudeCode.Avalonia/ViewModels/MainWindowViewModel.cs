using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Avalonia.Adapters;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Helper item for recent workspace menu.
/// </summary>
public partial class RecentWorkspaceMenuItem : ObservableObject
{
    public string DisplayName { get; init; } = "";
    public string Path { get; init; } = "";
    public required Action<string> OpenAction { get; init; }

    [RelayCommand]
    private void Open() => OpenAction(Path);
}

/// <summary>
/// Root ViewModel that orchestrates all panels and the engine lifecycle.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    // =========================================================================
    // Sub-ViewModels
    // =========================================================================
    
    // Layout ViewModels (VS Code style)
    public ActivityBarViewModel ActivityBar { get; } = new();
    public SidebarViewModel Sidebar { get; } = new();
    public EditorViewModel Editor { get; } = new();
    public PanelViewModel BottomPanel { get; } = new();
    public StatusBarViewModel StatusBar { get; } = new();
    public CommandPaletteViewModel CommandPalette { get; } = new();
    public NotificationViewModel Notification { get; } = new();
    
    // Panel ViewModels
    public ChatViewModel Chat { get; } = new();
    public AgentPanelViewModel AgentPanel { get; } = new();
    public TodoPanelViewModel TodoPanel { get; } = new();
    public ToolCallPanelViewModel ToolCallPanel { get; } = new();
    public SetupWizardViewModel SetupWizard { get; } = new();
    public QuestionDialogViewModel QuestionDialog { get; } = new();
    public FileExplorerViewModel FileExplorer { get; } = new();
    public TerminalViewModel Terminal { get; } = new();
    public SearchPanelViewModel Search { get; } = new();
    public ScmViewModel Scm { get; } = new();
    public ExtensionsViewModel Extensions { get; } = new();
    public DiffEditorViewModel DiffEditor { get; } = new();
    public GitHistoryViewModel GitHistory { get; } = new();
    public AuxiliaryBarViewModel AuxiliaryBar { get; } = new();
    public WorkspaceTrustViewModel WorkspaceTrust { get; } = new();
    public ComposerPanelViewModel ComposerPanel { get; } = new();

    // =========================================================================
    // Observable Properties
    // =========================================================================

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

    [ObservableProperty]
    private bool _isPlanMode;

    [ObservableProperty]
    private bool _hasWorkspace;

    // =========================================================================
    // Model Selector
    // =========================================================================

    public ObservableCollection<ModelOption> AvailableModels { get; } = [];

    [ObservableProperty]
    private ModelOption? _selectedModel;

    /// <summary>
    /// Whether the context row (plan badge, stats) should be visible.
    /// </summary>
    public bool ShowContextRow => IsPlanMode || HasWorkspace;

    public ObservableCollection<WorkspaceInfo> RecentWorkspaces { get; } = [];
    public ObservableCollection<RecentWorkspaceMenuItem> RecentWorkspaceMenuItems { get; } = [];

    // =========================================================================
    // Private State
    // =========================================================================

    private EngineContext? _engine;
    private bool _initialized;
    private CancellationTokenSource? _currentCts;
    private readonly WorkspaceService _workspaceService = new();
    private readonly FileWatcherService _fileWatcher = new();
    private Window? _mainWindow;

    public MainWindowViewModel()
    {
        Chat.MessageSubmitted += OnMessageSubmitted;
        TodoPanel.ExecutePlanRequested += OnExecutePlan;
        AgentPanel.LaunchAgentRequested += OnLaunchAgent;
        AgentPanel.ResumeAgentRequested += OnResumeAgent;
        LoadRecentWorkspacesList();
        
        // Wire up layout event handlers
        ActivityBar.ActivePanelChanged += OnActivityBarPanelChanged;
        Editor.CursorPositionChanged += (line, col) =>
        {
            StatusBar.CursorLine = line;
            StatusBar.CursorColumn = col;
        };
        Editor.ActiveFileChanged += (tab) =>
        {
            StatusBar.LanguageMode = tab?.Language ?? "Plain Text";
        };
        Editor.SaveError += (msg) => Notification.ShowError(msg);
        FileExplorer.FileViewRequested += (path) => Editor.OpenFile(path);
        Search.FileOpenRequested += (path, line) =>
        {
            Editor.OpenFile(path);
            Editor.GoToLine(line);
        };
        CommandPalette.FileOpenRequested += (path) => Editor.OpenFile(path);
        CommandPalette.GoToLineRequested += (line) =>
        {
            Editor.GoToLine(line);
        };
        Scm.DiffRequested += (relativePath) =>
        {
            var workDir = _engine?.WorkDir;
            if (!string.IsNullOrEmpty(workDir))
            {
                var fullPath = Path.Combine(workDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Editor.OpenDiff(fullPath, relativePath);
            }
        };
        Editor.DiffOpenRequested += (fullPath, relativePath, diff) =>
        {
            DiffEditor.LoadDiff(fullPath, relativePath, diff);
        };
        DiffEditor.CloseRequested += () =>
        {
            DiffEditor.IsVisible = false;
        };
        StatusBar.BranchClicked += OnBranchClicked;
        StatusBar.ProblemsClicked += () => BottomPanel.SwitchTabCommand.Execute("problems");
        StatusBar.NotificationsClicked += () => Notification.ToggleCenterCommand.Execute(null);
        
        // Set initial active panel
        Sidebar.SetActivePanel("explorer");
    }

    partial void OnIsPlanModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowContextRow));
        StatusBar.IsPlanMode = value;
    }
    
    partial void OnHasWorkspaceChanged(bool value) => OnPropertyChanged(nameof(ShowContextRow));

    private void OnActivityBarPanelChanged(string? panelId)
    {
        Sidebar.SetActivePanel(panelId);
    }

    /// <summary>
    /// Set the main window reference for dialogs.
    /// </summary>
    public void SetMainWindow(Window window) => _mainWindow = window;

    /// <summary>
    /// Initialize the engine asynchronously after the window is shown.
    /// </summary>
    public async void InitializeAsync(string[] args)
    {
        if (_initialized) return;
        _initialized = true;

        // Check for workspace path from command line args
        string? initialWorkspace = null;
        if (args.Length > 0 && Directory.Exists(args[0]))
            initialWorkspace = args[0];

        try
        {
            await InitializeEngineAsync(initialWorkspace);
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

    // =========================================================================
    // Engine Initialization
    // =========================================================================

    private async Task InitializeEngineAsync(string? workspacePath = null)
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
        var workDir = workspacePath ?? Directory.GetCurrentDirectory();

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
        _workspaceService.OpenFolder(workDir);

        // Wire up completion service and edit service to editor
        Editor.SetCompletionService(engine.CompletionService);
        Editor.InlineEdit.SetEditService(engine.EditService);

        // Wire up indexing services to chat mention picker
        Chat.SetIndexServices(engine.CodebaseIndex, engine.SymbolIndex);

        // Wire up composer service
        ComposerPanel.SetComposerService(engine.ComposerService);

        // Start background indexing
        _ = engine.CodebaseIndex.IndexWorkspaceAsync(workDir);

        DispatcherService.Post(() =>
        {
            ProviderDisplay = activeConfig.DisplayName;
            StatusBar.ProviderDisplay = activeConfig.DisplayName;
            HasWorkspace = true;
            UpdateTitle();
            StatusText = $"{activeConfig.DisplayName} | {workDir}";

            // Populate model selector
            PopulateAvailableModels();

            // Load file explorer
            FileExplorer.LoadWorkspace(workDir);

            // Set workspace for other panels
            Search.SetWorkspace(workDir);
            Scm.SetWorkspace(workDir);
            GitHistory.SetWorkspace(workDir);
            StatusBar.WorkspaceName = Path.GetFileName(workDir);

            // Start terminal in workspace
            Terminal.WorkingDirectory = workDir;

            // Start file watcher for auto-refresh
            _fileWatcher.Watch(workDir);
            _fileWatcher.FilesChanged += (changes) =>
            {
                // Auto-refresh file explorer and SCM on external changes
                FileExplorer.LoadWorkspace(workDir);
                _ = Scm.RefreshCommand.ExecuteAsync(null);
            };

            // Register command palette commands and populate file list
            RegisterPaletteCommands();
            PopulateFileList(workDir);

            // Initialize extensions
            _ = Extensions.DiscoverExtensionsCommand.ExecuteAsync(null);

            Chat.AddSystemMessage(
                $"MiniClaudeCode v0.3.0 | {activeConfig.DisplayName}\n" +
                $"Workspace: {workDir}\n" +
                "Type your message or use /help for commands. (Enter = send, Shift+Enter = newline)");

            LoadRecentWorkspacesList();
        });
    }

    // =========================================================================
    // Model Switching
    // =========================================================================

    private bool _suppressModelSwitch;

    /// <summary>
    /// Populate the model dropdown from configured providers.
    /// </summary>
    private void PopulateAvailableModels()
    {
        _suppressModelSwitch = true;
        AvailableModels.Clear();

        if (_engine == null)
        {
            _suppressModelSwitch = false;
            return;
        }

        ModelOption? activeOption = null;
        foreach (var (provider, config) in _engine.ProviderConfigs)
        {
            var option = new ModelOption(provider, config.ModelId, config.DisplayName);
            AvailableModels.Add(option);

            if (provider == _engine.ActiveConfig.Provider && config.ModelId == _engine.ActiveConfig.ModelId)
                activeOption = option;
        }

        SelectedModel = activeOption ?? (AvailableModels.Count > 0 ? AvailableModels[0] : null);
        _suppressModelSwitch = false;
    }

    partial void OnSelectedModelChanged(ModelOption? value)
    {
        if (_suppressModelSwitch || value == null || _engine == null) return;

        // Check if it's actually a different model
        if (value.Provider == _engine.ActiveConfig.Provider && value.ModelId == _engine.ActiveConfig.ModelId)
            return;

        // Switch the engine to the new provider/model
        _ = SwitchModelAsync(value);
    }

    private async Task SwitchModelAsync(ModelOption model)
    {
        if (_engine == null) return;

        CancelOperation();
        Chat.AddSystemMessage($"Switching to {model.DisplayName}...");

        try
        {
            var workDir = _engine.WorkDir;

            // Create adapters wired to ViewModels
            var outputSink = new AvaloniaOutputSink(Chat);
            var userInteraction = new AvaloniaUserInteraction(QuestionDialog);
            var progressReporter = new AvaloniaProgressReporter(AgentPanel, Chat);
            var toolCallObserver = new AvaloniaToolCallObserver(ToolCallPanel, Chat);

            // Rebuild engine with new active provider
            var engine = new EngineBuilder()
                .WithProviders(_engine.ProviderConfigs, model.Provider)
                .WithAgentProviderOverrides(_engine.AgentProviderOverrides)
                .WithWorkDir(workDir)
                .WithOutputSink(outputSink)
                .WithUserInteraction(userInteraction)
                .WithProgressReporter(progressReporter)
                .Build();

            var filter = new AvaloniaToolCallFilter(toolCallObserver, engine);
            engine.Kernel.AutoFunctionInvocationFilters.Add(filter);

            _engine = engine;

            // Wire up completion service and edit service to editor
            Editor.SetCompletionService(engine.CompletionService);
            Editor.InlineEdit.SetEditService(engine.EditService);

            // Wire up indexing services to chat mention picker
            Chat.SetIndexServices(engine.CodebaseIndex, engine.SymbolIndex);

            // Wire up composer service
            ComposerPanel.SetComposerService(engine.ComposerService);

            DispatcherService.Post(() =>
            {
                ProviderDisplay = model.DisplayName;
                StatusBar.ProviderDisplay = model.DisplayName;
                StatusText = $"{model.DisplayName} | {workDir}";
                UpdateTitle();
                Chat.AddSystemMessage($"Switched to {model.DisplayName}. Conversation reset.");
            });
        }
        catch (Exception ex)
        {
            DispatcherService.Post(() =>
                Chat.AddErrorMessage($"Model switch failed: {ex.Message}"));
        }
    }

    // =========================================================================
    // Workspace Management
    // =========================================================================

    [RelayCommand]
    private async Task OpenWorkspace()
    {
        if (_mainWindow == null) return;

        var folders = await _mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Workspace Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            await SwitchWorkspace(path);
        }
    }

    [RelayCommand]
    private async Task OpenRecent(string? path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            await SwitchWorkspace(path);
        }
    }

    private async Task SwitchWorkspace(string path)
    {
        // Cancel any running operation
        CancelOperation();

        // Reset state
        Chat.ClearMessages();
        AgentPanel.Clear();
        TodoPanel.Clear();
        ToolCallPanel.Clear();
        TurnCount = 0;
        ToolCallCount = 0;
        _engine = null;

        Chat.AddSystemMessage($"Switching to workspace: {path}");

        try
        {
            await InitializeEngineAsync(path);
        }
        catch (Exception ex)
        {
            Chat.AddErrorMessage($"Failed to open workspace: {ex.Message}");
        }
    }

    private void LoadRecentWorkspacesList()
    {
        var recent = WorkspaceService.GetRecentWorkspaces();

        RecentWorkspaces.Clear();
        RecentWorkspaceMenuItems.Clear();

        foreach (var ws in recent)
        {
            var info = new WorkspaceInfo
            {
                Path = ws.Path,
                Name = ws.Name,
                LastOpened = ws.LastOpened
            };
            RecentWorkspaces.Add(info);
            RecentWorkspaceMenuItems.Add(new RecentWorkspaceMenuItem
            {
                DisplayName = $"{info.DisplayName} - {ws.Path}",
                Path = ws.Path,
                OpenAction = p => _ = OpenRecent(p)
            });
        }
    }

    // =========================================================================
    // Message Processing
    // =========================================================================

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

        _currentCts = new CancellationTokenSource();
        var ct = _currentCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAgentMessageAsync(input, ct);
            }
            finally
            {
                DispatcherService.Post(() =>
                {
                    IsProcessing = false;
                    Chat.IsProcessing = false;
                });
            }
        }, ct);
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
                    "  /plan       - Toggle plan mode\n" +
                    "  /analyze    - Analyze project with multiple agents\n" +
                    "  /composer   - Open multi-file composer panel\n" +
                    "  /exit       - Exit application\n" +
                    "\n" +
                    "Keyboard:\n" +
                    "  Enter           - Send message\n" +
                    "  Shift+Enter     - New line\n" +
                    "  Ctrl+N          - New conversation\n" +
                    "  Ctrl+L          - Clear chat\n" +
                    "  Ctrl+O          - Open folder\n" +
                    "  Ctrl+`          - Toggle terminal\n" +
                    "  Ctrl+Shift+E    - Toggle explorer\n" +
                    "  Ctrl+Shift+P    - Toggle plan mode\n" +
                    "  Ctrl+Shift+I    - Open composer\n" +
                    "  Escape          - Cancel operation");
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
                        $"Agents: {_engine.AgentRegistry.Count} registered\n" +
                        $"Plan Mode: {(IsPlanMode ? "ON" : "OFF")}");
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

            case "/plan":
                TogglePlanMode();
                break;

            case "/analyze":
                RunAnalyzeProject();
                break;

            case "/composer":
                ComposerPanel.Show();
                Chat.AddSystemMessage("Composer panel opened. Describe your multi-file changes.");
                break;

            case "/exit" or "/quit" or "/q":
                Terminal.Dispose();
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

    private async Task ProcessAgentMessageAsync(string input, CancellationToken ct = default)
    {
        if (_engine == null) return;

        _engine.TurnCount++;
        DispatcherService.Post(() =>
        {
            TurnCount = _engine.TurnCount;
            ToolCallCount = _engine.TotalToolCalls;
            StatusBar.TurnCount = TurnCount;
            StatusBar.ToolCallCount = ToolCallCount;
            UpdateTitle();
        });

        try
        {
            // If plan mode is active, prepend plan mode instruction
            var effectiveInput = IsPlanMode
                ? $"[PLAN MODE - Read-only analysis only. Do NOT make changes. Present a structured plan.]\n\n{input}"
                : input;

            var message = new ChatMessageContent(AuthorRole.User, effectiveInput);

            // Start streaming - create placeholder message on UI thread
            DispatcherService.Post(() => Chat.BeginStreamingMessage());

            // Start elapsed timer
            var sw = Stopwatch.StartNew();
            var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = UpdateStreamingTimerAsync(sw, timerCts.Token);

            // Use InvokeStreamingAsync for true token-level streaming
            // This returns actual delta chunks from the provider (SSE stream)
            await foreach (var chunk in _engine.Agent.InvokeStreamingAsync(message, _engine.Thread).WithCancellation(ct))
            {
                var content = chunk.Message.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    DispatcherService.Post(() => Chat.AppendToStreaming(content));
                }
            }

            sw.Stop();
            timerCts.Cancel();

            DispatcherService.Post(() =>
            {
                Chat.EndStreaming(sw.Elapsed);
                ToolCallCount = _engine.TotalToolCalls;
                UpdateTitle();
                SyncTodos();
            });
        }
        catch (OperationCanceledException)
        {
            DispatcherService.Post(() =>
            {
                Chat.EndStreaming();
                Chat.AddSystemMessage("Operation cancelled.");
            });
        }
        catch (Exception ex)
        {
            DispatcherService.Post(() =>
            {
                Chat.EndStreaming();
                Chat.AddErrorMessage($"Error: {ex.Message}");
            });
        }
    }

    /// <summary>
    /// Periodically updates the streaming elapsed timer on the UI.
    /// </summary>
    private async Task UpdateStreamingTimerAsync(Stopwatch sw, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(200, ct);
                DispatcherService.Post(() => Chat.UpdateStreamingElapsed(sw.Elapsed));
            }
        }
        catch (OperationCanceledException) { }
    }

    // =========================================================================
    // Plan Mode
    // =========================================================================

    [RelayCommand]
    private void TogglePlanMode()
    {
        IsPlanMode = !IsPlanMode;
        TodoPanel.IsPlanMode = IsPlanMode;
        Chat.AddSystemMessage(IsPlanMode
            ? "Plan Mode: ON - Agent will analyze and plan without making changes."
            : "Plan Mode: OFF - Agent will execute changes normally.");
        UpdateTitle();
    }

    private void OnExecutePlan()
    {
        // Switch off plan mode and send plan context to the agent
        if (IsPlanMode)
        {
            IsPlanMode = false;
            TodoPanel.IsPlanMode = false;
            UpdateTitle();

            // Compose plan context from todos
            var planItems = TodoPanel.Todos
                .Select(t => $"- [{t.Status}] {t.Content}")
                .ToList();

            if (planItems.Count > 0)
            {
                var planContext = "Execute the following plan step by step:\n" + string.Join("\n", planItems);
                Chat.AddSystemMessage("Plan Mode: OFF - Executing plan...");

                // Submit the plan as a message
                IsProcessing = true;
                Chat.IsProcessing = true;
                Chat.AddUserMessage(planContext);

                _currentCts = new CancellationTokenSource();
                var ct = _currentCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessAgentMessageAsync(planContext, ct);
                    }
                    finally
                    {
                        DispatcherService.Post(() =>
                        {
                            IsProcessing = false;
                            Chat.IsProcessing = false;
                        });
                    }
                }, ct);
            }
            else
            {
                Chat.AddSystemMessage("No plan items to execute. Plan Mode: OFF.");
            }
        }
    }

    // =========================================================================
    // Multi-Agent Analysis
    // =========================================================================

    private void RunAnalyzeProject()
    {
        if (_engine == null)
        {
            Chat.AddSystemMessage("No engine available. Open a workspace first.");
            return;
        }

        IsProcessing = true;
        Chat.IsProcessing = true;
        Chat.AddSystemMessage("Analyzing project with multiple agents...");

        _ = Task.Run(async () =>
        {
            try
            {
                var tasks = new[]
                {
                    AnalyzeWithAgent("Architecture & Dependencies",
                        "Analyze the project architecture, directory structure, main entry points, and key dependencies. List the technology stack."),
                    AnalyzeWithAgent("Code Patterns & Conventions",
                        "Analyze coding patterns, naming conventions, design patterns used (MVVM, DI, etc.), and code style in this project."),
                    AnalyzeWithAgent("Public APIs & Entry Points",
                        "Identify all public APIs, entry points, services, and interfaces. Summarize what each major component does."),
                    AnalyzeWithAgent("Project Health",
                        "Assess project health: check for TODOs, potential issues, missing error handling, and suggest improvements.")
                };

                var results = await Task.WhenAll(tasks);

                DispatcherService.Post(() =>
                {
                    Chat.AddAssistantMessage(
                        "## Project Analysis Complete\n\n" +
                        string.Join("\n\n---\n\n", results.Where(r => !string.IsNullOrEmpty(r))));
                });
            }
            catch (Exception ex)
            {
                DispatcherService.Post(() =>
                    Chat.AddErrorMessage($"Analysis failed: {ex.Message}"));
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

    private async Task<string> AnalyzeWithAgent(string title, string prompt)
    {
        if (_engine == null) return "";

        try
        {
            var result = await _engine.SubAgentRunner.RunAsync(new MiniClaudeCode.Abstractions.Agents.AgentTask
            {
                Description = title,
                Prompt = prompt,
                AgentType = "explore",
                ModelTier = "fast",
                ReadOnly = true
            });

            return $"### {title}\n\n{result.Output}";
        }
        catch (Exception ex)
        {
            return $"### {title}\n\n*Error: {ex.Message}*";
        }
    }

    // =========================================================================
    // Manual Agent Launch / Resume
    // =========================================================================

    private void OnLaunchAgent(string agentType, string prompt)
    {
        if (_engine == null)
        {
            Chat.AddSystemMessage("No engine available. Open a workspace first.");
            return;
        }

        Chat.AddSystemMessage($"Launching {agentType} agent: {prompt}");

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _engine.SubAgentRunner.RunAsync(
                    new MiniClaudeCode.Abstractions.Agents.AgentTask
                    {
                        Description = prompt.Length > 50 ? prompt[..50] + "..." : prompt,
                        Prompt = prompt,
                        AgentType = agentType,
                        ReadOnly = agentType is "explore" or "plan"
                    });

                DispatcherService.Post(() =>
                {
                    Chat.AddAssistantMessage($"**Agent ({agentType}) Result:**\n\n{result.Output}");
                });
            }
            catch (Exception ex)
            {
                DispatcherService.Post(() =>
                    Chat.AddErrorMessage($"Agent failed: {ex.Message}"));
            }
        });
    }

    private void OnResumeAgent(string agentId)
    {
        if (_engine == null)
        {
            Chat.AddSystemMessage("No engine available.");
            return;
        }

        Chat.AddSystemMessage($"Resuming agent {agentId}...");

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _engine.SubAgentRunner.RunAsync(
                    new MiniClaudeCode.Abstractions.Agents.AgentTask
                    {
                        Description = $"Resume agent {agentId}",
                        Prompt = "Continue from where you left off.",
                        AgentType = "generalPurpose",
                        ResumeAgentId = agentId
                    });

                DispatcherService.Post(() =>
                {
                    Chat.AddAssistantMessage($"**Resumed Agent Result:**\n\n{result.Output}");
                });
            }
            catch (Exception ex)
            {
                DispatcherService.Post(() =>
                    Chat.AddErrorMessage($"Agent resume failed: {ex.Message}"));
            }
        });
    }

    // =========================================================================
    // Cancellation
    // =========================================================================

    [RelayCommand]
    private void CancelOperation()
    {
        _currentCts?.Cancel();
        _currentCts = null;
    }

    // =========================================================================
    // Terminal
    // =========================================================================

    [RelayCommand]
    private void ToggleTerminal()
    {
        Terminal.IsVisible = !Terminal.IsVisible;
    }

    // =========================================================================
    // Utility
    // =========================================================================

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
        var planIndicator = IsPlanMode ? " [PLAN]" : "";
        WindowTitle = _engine != null
            ? $"MiniClaudeCode - {_engine.ActiveConfig.DisplayName} | Turn:{TurnCount} Tools:{ToolCallCount}{planIndicator}"
            : "MiniClaudeCode v0.3.0 - Avalonia";
    }

    // =========================================================================
    // Branch Picker
    // =========================================================================

    private async void OnBranchClicked()
    {
        if (_engine == null) return;

        try
        {
            var branches = await Scm.GetBranchListAsync();
            if (branches.Count == 0)
            {
                Notification.ShowInfo("No branches found.");
                return;
            }

            // Populate command palette with branches for quick selection
            CommandPalette.FilteredItems.Clear();
            foreach (var branch in branches)
            {
                var branchName = branch;
                CommandPalette.FilteredItems.Add(new CommandItem
                {
                    Id = $"branch_{branchName}",
                    Label = branchName,
                    Category = "Switch Branch",
                    Execute = () => _ = SwitchBranchAsync(branchName)
                });
            }

            CommandPalette.IsVisible = true;
            CommandPalette.QueryText = "";
            CommandPalette.Placeholder = "Select branch to switch to...";
            CommandPalette.ModeIndicator = "\u2387"; // branch icon
        }
        catch (Exception ex)
        {
            Notification.ShowError($"Failed to load branches: {ex.Message}");
        }
    }

    private async Task SwitchBranchAsync(string branchName)
    {
        try
        {
            var success = await Scm.CheckoutBranchAsync(branchName);
            if (success)
            {
                Notification.ShowInfo($"Switched to branch: {branchName}");
                StatusBar.BranchName = branchName;
            }
            else
            {
                Notification.ShowError($"Failed to switch to branch: {branchName}");
            }
        }
        catch (Exception ex)
        {
            Notification.ShowError($"Branch switch failed: {ex.Message}");
        }
    }

    // =========================================================================
    // Command Palette Registration
    // =========================================================================

    private void RegisterPaletteCommands()
    {
        CommandPalette.RegisterCommands(
        [
            new() { Id = "new_conversation", Label = "New Conversation", Category = "Chat", Shortcut = "Ctrl+N", Execute = () => HandleSlashCommand("/new") },
            new() { Id = "clear_chat", Label = "Clear Chat", Category = "Chat", Shortcut = "Ctrl+L", Execute = () => HandleSlashCommand("/clear") },
            new() { Id = "show_status", Label = "Show Status", Category = "Info", Execute = () => HandleSlashCommand("/status") },
            new() { Id = "toggle_plan", Label = "Toggle Plan Mode", Category = "Mode", Shortcut = "Ctrl+Shift+P", Execute = () => HandleSlashCommand("/plan") },
            new() { Id = "analyze_project", Label = "Analyze Project", Category = "Agent", Execute = () => HandleSlashCommand("/analyze") },
            new() { Id = "compact", Label = "Compact Conversation", Category = "Chat", Execute = () => HandleSlashCommand("/compact") },
            new() { Id = "open_composer", Label = "Open Composer", Category = "AI", Shortcut = "Ctrl+Shift+I", Execute = () => ComposerPanel.Show() },
            new() { Id = "open_workspace", Label = "Open Folder...", Category = "File", Shortcut = "Ctrl+O", Execute = () => _ = OpenWorkspace() },
            new() { Id = "toggle_terminal", Label = "Toggle Terminal", Category = "View", Shortcut = "Ctrl+`", Execute = () => ToggleTerminal() },
            new() { Id = "toggle_sidebar", Label = "Toggle Sidebar", Category = "View", Execute = () => Sidebar.IsVisible = !Sidebar.IsVisible },
            new() { Id = "show_explorer", Label = "Show Explorer", Category = "View", Shortcut = "Ctrl+Shift+E", Execute = () => { ActivityBar.ActivatePanel("explorer"); Sidebar.SetActivePanel("explorer"); } },
            new() { Id = "show_search", Label = "Show Search", Category = "View", Shortcut = "Ctrl+Shift+F", Execute = () => { ActivityBar.ActivatePanel("search"); Sidebar.SetActivePanel("search"); } },
            new() { Id = "show_scm", Label = "Show Source Control", Category = "View", Shortcut = "Ctrl+Shift+G", Execute = () => { ActivityBar.ActivatePanel("scm"); Sidebar.SetActivePanel("scm"); } },
            new() { Id = "show_extensions", Label = "Show Extensions", Category = "View", Shortcut = "Ctrl+Shift+X", Execute = () => { ActivityBar.ActivatePanel("extensions"); Sidebar.SetActivePanel("extensions"); } },
            new() { Id = "show_chat", Label = "Show AI Chat", Category = "View", Shortcut = "Ctrl+Shift+C", Execute = () => { ActivityBar.ActivatePanel("chat"); Sidebar.SetActivePanel("chat"); } },
            new() { Id = "close_editor", Label = "Close Editor", Category = "Editor", Shortcut = "Ctrl+W", Execute = () => Editor.CloseAllTabsCommand.Execute(null) },
            new() { Id = "split_editor", Label = "Split Editor Right", Category = "Editor", Shortcut = @"Ctrl+\", Execute = () => Editor.SplitEditorCommand.Execute(null) },
            new() { Id = "unsplit_editor", Label = "Close Split Editor", Category = "Editor", Execute = () => Editor.UnsplitEditorCommand.Execute(null) },
            new() { Id = "save_file", Label = "Save File", Category = "File", Shortcut = "Ctrl+S", Execute = () => Editor.SaveFileCommand.Execute(null) },
            new() { Id = "inline_edit", Label = "Inline Edit (AI)", Category = "Editor", Shortcut = "Ctrl+K", Execute = () => { /* Handled by EditorView directly */ } },
            new() { Id = "git_commit", Label = "Git: Commit", Category = "Git", Execute = () => _ = Scm.CommitCommand.ExecuteAsync(null) },
            new() { Id = "git_pull", Label = "Git: Pull", Category = "Git", Execute = () => _ = Scm.PullCommand.ExecuteAsync(null) },
            new() { Id = "git_push", Label = "Git: Push", Category = "Git", Execute = () => _ = Scm.PushCommand.ExecuteAsync(null) },
            new() { Id = "git_fetch", Label = "Git: Fetch", Category = "Git", Execute = () => _ = Scm.FetchCommand.ExecuteAsync(null) },
            new() { Id = "git_history", Label = "Git: Show History", Category = "Git", Execute = () => GitHistory.ShowCommand.Execute(null) },
            new() { Id = "toggle_auxiliary", Label = "Toggle Auxiliary Bar", Category = "View", Shortcut = "Ctrl+Alt+B", Execute = () => AuxiliaryBar.ToggleCommand.Execute(null) },
            new() { Id = "workspace_trust", Label = "Workspace Trust: Manage", Category = "Security", Execute = () => WorkspaceTrust.ShowDialogCommand.Execute(null) },
            new() { Id = "exit_app", Label = "Exit", Category = "Application", Execute = () => HandleSlashCommand("/exit") },
        ]);
    }

    private void PopulateFileList(string workspacePath)
    {
        try
        {
            var files = new List<string>();
            CollectFiles(workspacePath, files, 0, maxDepth: 8, maxFiles: 5000);
            CommandPalette.SetFileList(files.Select(f =>
                Path.GetRelativePath(workspacePath, f)));
        }
        catch
        {
            // Silently ignore file collection errors
        }
    }

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", ".vs", ".idea", "__pycache__",
        ".next", "dist", "build", ".cache", "packages", ".nuget"
    };

    private static void CollectFiles(string dir, List<string> files, int depth, int maxDepth, int maxFiles)
    {
        if (depth > maxDepth || files.Count >= maxFiles) return;

        try
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                if (files.Count >= maxFiles) return;
                files.Add(file);
            }

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var name = Path.GetFileName(subDir);
                if (SkipDirs.Contains(name)) continue;
                CollectFiles(subDir, files, depth + 1, maxDepth, maxFiles);
            }
        }
        catch
        {
            // Permission denied, etc.
        }
    }

    [RelayCommand]
    private void NewConversation() => HandleSlashCommand("/new");

    [RelayCommand]
    private void ClearChat() => Chat.ClearMessages();

    [RelayCommand]
    private void ShowStatus() => HandleSlashCommand("/status");

    [RelayCommand]
    private void ExitApp() => HandleSlashCommand("/exit");

    [RelayCommand]
    private void OpenComposer() => ComposerPanel.Show();

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
