using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class ClearCommand : ISlashCommand
{
    public string Name => "clear";
    public string[] Aliases => ["cls"];
    public string Description => "Clear the terminal screen";

    public Task ExecuteAsync(string args, CliContext context)
    {
        AnsiConsole.Clear();
        return Task.CompletedTask;
    }
}
