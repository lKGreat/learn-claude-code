using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class ExitCommand : ISlashCommand
{
    public string Name => "exit";
    public string[] Aliases => ["quit", "q"];
    public string Description => "Exit the CLI";

    public Task ExecuteAsync(string args, CliContext context)
    {
        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
        context.ExitRequested = true;
        return Task.CompletedTask;
    }
}
