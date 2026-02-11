using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Cli.Commands;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Interactive REPL loop with Spectre.Console rendering.
///
/// Features:
///   - Branded startup banner
///   - Slash command routing with tab completion
///   - Tool call visualization (via ToolCallVisualizer filter)
///   - Color-coded output rendering
///   - Turn counting
///   - Robust Ctrl+C handling:
///     · During input: cancels current line (double Ctrl+C to exit)
///     · During agent execution: cancels the operation gracefully
/// </summary>
public class ReplLoop
{
    private readonly CliContext _ctx;
    private readonly SlashCommandRouter _router;
    private readonly LineEditor _lineEditor;

    /// <summary>Prompt markup shown before each input line.</summary>
    private const string Prompt = "[bold cyan]>> [/]";

    public ReplLoop(CliContext ctx)
    {
        _ctx = ctx;
        _router = new SlashCommandRouter();
        _lineEditor = new LineEditor();
        RegisterCommands();
        RegisterCompletions();
    }

    private void RegisterCommands()
    {
        _router.Register(new HelpCommand(_router));
        _router.Register(new ModelCommand());
        _router.Register(new ProviderCommand());
        _router.Register(new StatusCommand());
        _router.Register(new ClearCommand());
        _router.Register(new DiffCommand());
        _router.Register(new CostCommand());
        _router.Register(new NewCommand());
        _router.Register(new ExitCommand());
    }

    /// <summary>
    /// Register all slash commands (with "/" prefix) as completions for the LineEditor.
    /// </summary>
    private void RegisterCompletions()
    {
        var completions = new List<string>();
        foreach (var cmd in _router.All)
        {
            completions.Add($"/{cmd.Name}");
            foreach (var alias in cmd.Aliases)
                completions.Add($"/{alias}");
        }
        _lineEditor.SetCompletions(completions);
    }

    /// <summary>
    /// Run the interactive REPL loop.
    /// </summary>
    /// <param name="initialPrompt">Optional initial prompt to execute immediately.</param>
    public async Task RunAsync(string? initialPrompt = null)
    {
        // Enable TreatControlCAsInput so our LineEditor can handle Ctrl+C
        // This prevents the default behavior of killing the process on Ctrl+C
        try { Console.TreatControlCAsInput = true; } catch { /* Ignore if unsupported */ }

        // Show banner
        Banner.Render(_ctx);

        // If there's an initial prompt, process it first
        if (!string.IsNullOrWhiteSpace(initialPrompt))
        {
            await ProcessUserInput(initialPrompt);
        }

        // Main REPL loop
        while (!_ctx.ExitRequested)
        {
            var input = _lineEditor.ReadLine(Prompt);

            if (input == null || _lineEditor.ExitRequested)
                break; // Double Ctrl+C exit

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Check for bare exit commands (without / prefix)
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input == "q")
            {
                break;
            }

            await ProcessUserInput(input);
        }
    }

    /// <summary>
    /// Process a single user input: route to slash commands or send to agent.
    /// </summary>
    private async Task ProcessUserInput(string input)
    {
        // Try slash command first
        if (await _router.TryRouteAsync(input, _ctx))
        {
            AnsiConsole.WriteLine();
            return;
        }

        // Send to agent
        _ctx.TurnCount++;

        // Create a cancellation token source for this execution
        using var cts = new CancellationTokenSource();

        // Start a background task that watches for Ctrl+C during execution
        var watcherTask = WatchForCancelKeyAsync(cts);

        try
        {
            var message = new ChatMessageContent(AuthorRole.User, input);
            string lastContent = "";

            await foreach (var response in _ctx.Agent.InvokeAsync(message, _ctx.Thread).WithCancellation(cts.Token))
            {
                if (!string.IsNullOrEmpty(response.Message.Content))
                {
                    lastContent = response.Message.Content;
                }
            }

            // Render the final assistant message
            if (!string.IsNullOrEmpty(lastContent))
            {
                AnsiConsole.WriteLine();
                OutputRenderer.RenderAssistantMessage(lastContent);
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Operation cancelled by user.[/]");
        }
        catch (Exception ex)
        {
            OutputRenderer.RenderError(ex.Message);
        }
        finally
        {
            // Stop the key watcher
            await cts.CancelAsync();
            try { await watcherTask; } catch { /* Ignore */ }

            // Drain any buffered keys so they don't leak into the next input
            DrainKeyBuffer();
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Background task that watches for Ctrl+C key presses during agent execution.
    /// When detected, cancels the provided CancellationTokenSource.
    /// </summary>
    private static Task WatchForCancelKeyAsync(CancellationTokenSource cts)
    {
        return Task.Run(() =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        {
                            cts.Cancel();
                            return;
                        }
                    }
                    Thread.Sleep(50); // Poll every 50ms
                }
            }
            catch
            {
                // Ignore errors (e.g. ObjectDisposedException when cts is disposed)
            }
        });
    }

    /// <summary>
    /// Drain any remaining keys in the console input buffer.
    /// This prevents stale keypresses from leaking into the next ReadLine.
    /// </summary>
    private static void DrainKeyBuffer()
    {
        try
        {
            while (Console.KeyAvailable)
                Console.ReadKey(true);
        }
        catch { /* Ignore */ }
    }
}
