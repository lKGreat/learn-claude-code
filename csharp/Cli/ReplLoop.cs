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
///   - Slash command routing
///   - Tool call visualization (via ToolCallVisualizer filter)
///   - Color-coded output rendering
///   - Turn counting
/// </summary>
public class ReplLoop
{
    private readonly CliContext _ctx;
    private readonly SlashCommandRouter _router;

    public ReplLoop(CliContext ctx)
    {
        _ctx = ctx;
        _router = new SlashCommandRouter();
        RegisterCommands();
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
    /// Run the interactive REPL loop.
    /// </summary>
    /// <param name="initialPrompt">Optional initial prompt to execute immediately.</param>
    public async Task RunAsync(string? initialPrompt = null)
    {
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
            var input = ReadInput();

            if (input == null)
                break; // EOF / Ctrl+C

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Check for bare exit commands
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
    /// Read user input with a styled prompt.
    /// </summary>
    private static string? ReadInput()
    {
        AnsiConsole.Markup("[bold cyan]>> [/]");
        try
        {
            return Console.ReadLine()?.Trim();
        }
        catch
        {
            return null;
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

        try
        {
            var message = new ChatMessageContent(AuthorRole.User, input);
            string lastContent = "";

            await foreach (var response in _ctx.Agent.InvokeAsync(message, _ctx.Thread))
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
        catch (Exception ex)
        {
            OutputRenderer.RenderError(ex.Message);
        }

        AnsiConsole.WriteLine();
    }
}
