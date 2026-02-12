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

            // --- Build TUI views first (needed for adapters) ---
            // Create a temporary null-output engine to get the views set up,
            // then rebuild with proper adapters.

            var tempMainWindow = new MainWindow(new EngineBuilder()
                .WithProviders(providerConfigs, activeProvider)
                .WithAgentProviderOverrides(agentProviderOverrides)
                .WithWorkDir(workDir)
                .WithUserInteraction(new TuiUserInteraction())
                .Build());

            // Create adapters wired to views
            var outputSink = new TuiOutputSink(tempMainWindow.ChatView);
            var userInteraction = new TuiUserInteraction();
            var progressReporter = new TuiAgentProgressReporter(tempMainWindow.AgentPanel, tempMainWindow.ChatView);
            var toolCallObserver = new TuiToolCallObserver(tempMainWindow.ChatView, tempMainWindow.ToolCallPanel);

            // --- Build engine with proper adapters ---
            var engine = new EngineBuilder()
                .WithProviders(providerConfigs, activeProvider)
                .WithAgentProviderOverrides(agentProviderOverrides)
                .WithWorkDir(workDir)
                .WithOutputSink(outputSink)
                .WithUserInteraction(userInteraction)
                .WithProgressReporter(progressReporter)
                .Build();

            // --- Build proper MainWindow ---
            var mainWindow = new MainWindow(engine);

            // Re-wire adapters to the real window's views
            var realOutputSink = new TuiOutputSink(mainWindow.ChatView);
            var realProgressReporter = new TuiAgentProgressReporter(mainWindow.AgentPanel, mainWindow.ChatView);
            var realToolCallObserver = new TuiToolCallObserver(mainWindow.ChatView, mainWindow.ToolCallPanel);

            // Register tool call filter
            var filter = new TuiToolCallFilter(realToolCallObserver, engine);
            engine.Kernel.AutoFunctionInvocationFilters.Add(filter);

            // --- Wire up input handling ---
            mainWindow.InputSubmitted += async (input) =>
            {
                await ProcessUserInputAsync(engine, mainWindow, input);
            };

            // --- Create menu bar and status bar ---
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
                    var msg = $"Provider: {engine.ActiveConfig.DisplayName}\n" +
                              $"Workspace: {engine.WorkDir}\n" +
                              $"Turns: {engine.TurnCount}\n" +
                              $"Tool Calls: {engine.TotalToolCalls}\n" +
                              $"Agents: {engine.AgentRegistry.Count} registered";
                    MessageBox.Query("Session Status", msg, "OK");
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
                $"Type your message below or use F-key shortcuts.");

            // --- Print mode ---
            if (cliArgs.Mode == CliMode.Print && !string.IsNullOrEmpty(cliArgs.Prompt))
            {
                Application.Shutdown();
                await RunPrintMode(engine, cliArgs.Prompt);
                return;
            }

            // --- Handle initial prompt ---
            if (!string.IsNullOrWhiteSpace(cliArgs.Prompt))
            {
                _ = Task.Run(async () =>
                {
                    await ProcessUserInputAsync(engine, mainWindow, cliArgs.Prompt);
                });
            }

            // --- Run TUI ---
            var top = new Toplevel();
            top.Add(menuBar, mainWindow);
            Application.Run(top);
            top.Dispose();
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private static async Task ProcessUserInputAsync(EngineContext engine, MainWindow mainWindow, string input)
    {
        engine.TurnCount++;

        Application.Invoke(() =>
        {
            mainWindow.ChatView.AddUserMessage(input);
            mainWindow.UpdateTitle();
        });

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
