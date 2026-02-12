using Terminal.Gui;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Core.Configuration;
using MiniClaudeCode.Tui.Adapters;
using MiniClaudeCode.Tui.Dialogs;
using MiniClaudeCode.Tui.Views;

namespace MiniClaudeCode.Tui.App;

/// <summary>
/// Main TUI application lifecycle.
/// Bootstraps configuration, builds the engine via EngineBuilder,
/// then runs the Terminal.Gui application loop.
/// </summary>
public class TuiApp
{
    private bool _isProcessing;

    public async Task RunAsync(CliArgs cliArgs)
    {
        // --- Version ---
        if (cliArgs.ShowVersion)
        {
            Console.WriteLine($"MiniClaudeCode v{CliArgs.Version}");
            return;
        }

        // --- Load .env ---
        LoadEnvFiles();

        // --- Load providers ---
        var providerConfigs = ModelProviderConfig.LoadAll();

        // --- Initialize Terminal.Gui ---
        Application.Init();

        try
        {
            if (providerConfigs.Count == 0)
            {
                if (cliArgs.Mode == CliMode.Interactive)
                {
                    providerConfigs = await SetupWizardDialog.RunAsync();
                    if (providerConfigs.Count == 0)
                    {
                        Application.Shutdown();
                        return;
                    }
                }
                else
                {
                    Application.Shutdown();
                    Console.WriteLine("Error: No model provider configured.");
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
                    Application.Shutdown();
                    Console.WriteLine($"Unknown provider: {cliArgs.ProviderOverride}");
                    return;
                }
            }
            else
            {
                activeProvider = ModelProviderConfig.ResolveActiveProvider(providerConfigs);
            }

            var activeConfig = providerConfigs[activeProvider];
            if (!string.IsNullOrEmpty(cliArgs.ModelOverride))
                activeConfig.ModelId = cliArgs.ModelOverride;

            var agentProviderOverrides = ModelProviderConfig.LoadAgentProviderOverrides(providerConfigs);

            // --- Resolve working directory ---
            var workDir = ResolveWorkingDirectory(cliArgs);

            // --- Print mode (no TUI needed) ---
            if (cliArgs.Mode == CliMode.Print && !string.IsNullOrEmpty(cliArgs.Prompt))
            {
                Application.Shutdown();
                var printEngine = new EngineBuilder()
                    .WithProviders(providerConfigs, activeProvider)
                    .WithAgentProviderOverrides(agentProviderOverrides)
                    .WithWorkDir(workDir)
                    .WithUserInteraction(new TuiUserInteraction())
                    .Build();
                await RunPrintMode(printEngine, cliArgs.Prompt);
                return;
            }

            // =====================================================
            // Build views -> adapters -> engine (correct order!)
            // =====================================================

            // Step 1: Create the MainWindow (views only, no engine yet)
            var mainWindow = new MainWindow(activeConfig.DisplayName);

            // Step 2: Create adapters wired to the REAL views
            var outputSink = new TuiOutputSink(mainWindow.ChatView);
            var userInteraction = new TuiUserInteraction();
            var progressReporter = new TuiAgentProgressReporter(mainWindow.AgentPanel, mainWindow.ChatView);
            var toolCallObserver = new TuiToolCallObserver(mainWindow.ChatView, mainWindow.ToolCallPanel);

            // Step 3: Build engine with the correct adapters
            var engine = new EngineBuilder()
                .WithProviders(providerConfigs, activeProvider)
                .WithAgentProviderOverrides(agentProviderOverrides)
                .WithWorkDir(workDir)
                .WithOutputSink(outputSink)
                .WithUserInteraction(userInteraction)
                .WithProgressReporter(progressReporter)
                .Build();

            // Step 4: Connect engine to MainWindow
            mainWindow.Engine = engine;

            // Register tool call filter
            var filter = new TuiToolCallFilter(toolCallObserver, engine);
            engine.Kernel.AutoFunctionInvocationFilters.Add(filter);

            // --- Wire up input handling ---
            mainWindow.InputSubmitted += input => OnInputSubmitted(engine, mainWindow, input);

            // --- Create menu bar ---
            var menuBar = mainWindow.CreateMenuBar(
                onNew: () =>
                {
                    engine.ResetThread();
                    mainWindow.ChatView.ClearChat();
                    mainWindow.ChatView.AddSystemMessage("Started a new conversation.");
                    mainWindow.UpdateTitle();
                },
                onClear: () =>
                {
                    mainWindow.ChatView.ClearChat();
                },
                onStatus: () =>
                {
                    ShowStatus(engine, mainWindow);
                },
                onExit: () =>
                {
                    Application.RequestStop();
                }
            );

            // Show welcome message
            mainWindow.ChatView.AddSystemMessage(
                $"MiniClaudeCode v{CliArgs.Version} | {engine.ActiveConfig.DisplayName}\n" +
                $"Workspace: {engine.WorkDir}\n" +
                $"Type your message or use /help for commands. (Enter=send, Shift+Enter=newline)");

            // --- Build Toplevel ---
            var top = new Toplevel();
            top.Add(menuBar, mainWindow);

            // --- Set focus AFTER the view hierarchy is ready ---
            top.Ready += (_, _) =>
            {
                mainWindow.InputView.FocusInput();
            };

            // --- Handle initial prompt ---
            if (!string.IsNullOrWhiteSpace(cliArgs.Prompt))
            {
                top.Ready += (_, _) =>
                {
                    OnInputSubmitted(engine, mainWindow, cliArgs.Prompt);
                };
            }

            // --- Run TUI (blocks until exit) ---
            Application.Run(top);
            top.Dispose();
        }
        finally
        {
            Application.Shutdown();
        }
    }

    /// <summary>
    /// Handle user input - dispatch slash commands or send to agent.
    /// </summary>
    private void OnInputSubmitted(EngineContext engine, MainWindow mainWindow, string input)
    {
        if (_isProcessing)
        {
            mainWindow.ChatView.AddSystemMessage("Please wait - still processing...");
            return;
        }

        // --- Slash commands ---
        if (input.StartsWith('/'))
        {
            HandleSlashCommand(engine, mainWindow, input);
            return;
        }

        // --- Agent message ---
        _isProcessing = true;
        mainWindow.ChatView.AddUserMessage(input);

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAgentMessageAsync(engine, mainWindow, input);
            }
            finally
            {
                _isProcessing = false;
            }
        });
    }

    /// <summary>
    /// Process slash commands.
    /// </summary>
    private static void HandleSlashCommand(EngineContext engine, MainWindow mainWindow, string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "/help" or "/h" or "/?":
                mainWindow.ChatView.AddSystemMessage(
                    "Available commands:\n" +
                    "  /help       - Show this help\n" +
                    "  /new        - Start new conversation (F5)\n" +
                    "  /clear      - Clear chat display (F6)\n" +
                    "  /status     - Show session status (F4)\n" +
                    "  /model      - Show current model info\n" +
                    "  /compact    - Summarize conversation (saves context)\n" +
                    "  /exit       - Exit application (F10)\n" +
                    "\n" +
                    "Keyboard:\n" +
                    "  Enter       - Send message\n" +
                    "  Shift+Enter - New line\n" +
                    "  Tab         - Switch focus between panels");
                break;

            case "/new" or "/reset":
                engine.ResetThread();
                mainWindow.ChatView.ClearChat();
                mainWindow.ChatView.AddSystemMessage("Started a new conversation.");
                mainWindow.UpdateTitle();
                break;

            case "/clear" or "/cls":
                mainWindow.ChatView.ClearChat();
                break;

            case "/status" or "/stat":
                ShowStatus(engine, mainWindow);
                break;

            case "/model":
                mainWindow.ChatView.AddSystemMessage(
                    $"Provider: {engine.ActiveConfig.Provider}\n" +
                    $"Model: {engine.ActiveConfig.ModelId}\n" +
                    $"Display: {engine.ActiveConfig.DisplayName}");
                break;

            case "/compact":
                mainWindow.ChatView.AddSystemMessage("Compacting conversation...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var summary = await CompactConversationAsync(engine);
                        Application.Invoke(() =>
                        {
                            mainWindow.ChatView.ClearChat();
                            mainWindow.ChatView.AddSystemMessage($"Conversation compacted. Summary:\n{summary}");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Invoke(() =>
                        {
                            mainWindow.ChatView.AddSystemMessage($"Compact failed: {ex.Message}");
                        });
                    }
                });
                break;

            case "/exit" or "/quit" or "/q":
                Application.RequestStop();
                break;

            default:
                mainWindow.ChatView.AddSystemMessage($"Unknown command: {command}. Type /help for available commands.");
                break;
        }
    }

    /// <summary>
    /// Show status in a dialog.
    /// </summary>
    private static void ShowStatus(EngineContext engine, MainWindow mainWindow)
    {
        var msg = $"Provider: {engine.ActiveConfig.DisplayName}\n" +
                  $"Model: {engine.ActiveConfig.ModelId}\n" +
                  $"Workspace: {engine.WorkDir}\n" +
                  $"Turns: {engine.TurnCount}\n" +
                  $"Tool Calls: {engine.TotalToolCalls}\n" +
                  $"Agents: {engine.AgentRegistry.Count} registered";
        MessageBox.Query("Session Status", msg, "OK");
    }

    /// <summary>
    /// Send a message to the agent and display the response.
    /// </summary>
    private static async Task ProcessAgentMessageAsync(EngineContext engine, MainWindow mainWindow, string input)
    {
        engine.TurnCount++;

        Application.Invoke(() => mainWindow.UpdateTitle());

        try
        {
            var message = new ChatMessageContent(AuthorRole.User, input);
            string lastContent = "";

            await foreach (var response in engine.Agent.InvokeAsync(message, engine.Thread))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                    lastContent = response.Message.Content;
            }

            if (!string.IsNullOrEmpty(lastContent))
            {
                Application.Invoke(() =>
                {
                    mainWindow.ChatView.AddAssistantMessage(lastContent);
                    mainWindow.UpdateTitle();
                    mainWindow.InputView.FocusInput();
                });
            }
        }
        catch (OperationCanceledException)
        {
            Application.Invoke(() =>
            {
                mainWindow.ChatView.AddSystemMessage("Operation cancelled.");
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                mainWindow.ChatView.AddSystemMessage($"Error: {ex.Message}");
            });
        }
    }

    /// <summary>
    /// Compact the conversation by summarizing it.
    /// </summary>
    private static async Task<string> CompactConversationAsync(EngineContext engine)
    {
        var compactMessage = new ChatMessageContent(AuthorRole.User,
            "Please summarize our conversation so far in a concise paragraph. " +
            "Include key decisions, code changes made, and any pending tasks.");

        string summary = "";
        await foreach (var response in engine.Agent.InvokeAsync(compactMessage, engine.Thread))
        {
            if (!string.IsNullOrEmpty(response.Message.Content))
                summary = response.Message.Content;
        }

        // Reset thread and add the summary as context
        engine.ResetThread();
        return summary;
    }

    private static async Task RunPrintMode(EngineContext engine, string prompt)
    {
        var message = new ChatMessageContent(AuthorRole.User, prompt);
        await foreach (var response in engine.Agent.InvokeAsync(message, engine.Thread))
        {
            if (!string.IsNullOrEmpty(response.Message.Content))
                Console.WriteLine(response.Message.Content);
        }
    }

    private static string ResolveWorkingDirectory(CliArgs cliArgs)
    {
        var cwd = Directory.GetCurrentDirectory();

        if (cliArgs.AddDirs.Count > 0)
        {
            var dir = Path.GetFullPath(cliArgs.AddDirs[0]);
            if (Directory.Exists(dir))
            {
                Directory.SetCurrentDirectory(dir);
                return dir;
            }
        }

        var isSysDir = cwd.Contains("System32", StringComparison.OrdinalIgnoreCase)
                    || cwd.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase);

        if (isSysDir)
        {
            var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            Directory.SetCurrentDirectory(exeDir);
            return exeDir;
        }

        return cwd;
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
