using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class NewCommand : ISlashCommand
{
    public string Name => "new";
    public string Description => "Start a new conversation";

    public Task ExecuteAsync(string args, CliContext context)
    {
        context.ResetThread();
        AnsiConsole.MarkupLine("[green]Started a new conversation.[/]");
        return Task.CompletedTask;
    }
}
