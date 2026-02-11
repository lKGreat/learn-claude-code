using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

/// <summary>
/// Routes slash commands (/name args) to the matching ISlashCommand handler.
/// </summary>
public class SlashCommandRouter
{
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISlashCommand> _allCommands = [];

    /// <summary>
    /// Register a slash command.
    /// </summary>
    public void Register(ISlashCommand command)
    {
        _allCommands.Add(command);
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
            _commands[alias] = command;
    }

    /// <summary>
    /// Get all registered commands (for /help).
    /// </summary>
    public IReadOnlyList<ISlashCommand> All => _allCommands;

    /// <summary>
    /// Try to route a user input to a slash command.
    /// Returns true if the input was a slash command (even if invalid).
    /// </summary>
    public async Task<bool> TryRouteAsync(string input, CliContext context)
    {
        if (string.IsNullOrEmpty(input) || input[0] != '/')
            return false;

        // Parse: /command args...
        var spaceIdx = input.IndexOf(' ', 1);
        var cmdName = spaceIdx > 0 ? input[1..spaceIdx] : input[1..];
        var args = spaceIdx > 0 ? input[(spaceIdx + 1)..].Trim() : "";

        if (_commands.TryGetValue(cmdName, out var command))
        {
            try
            {
                await command.ExecuteAsync(args, context);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Command error: {Markup.Escape(ex.Message)}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Unknown command:[/] /{Markup.Escape(cmdName)}");
            AnsiConsole.MarkupLine("[dim]Type /help for available commands.[/]");
        }

        return true;
    }
}
