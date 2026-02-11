using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class HelpCommand(SlashCommandRouter router) : ISlashCommand
{
    public string Name => "help";
    public string[] Aliases => ["h", "?"];
    public string Description => "Show available commands";

    public Task ExecuteAsync(string args, CliContext context)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold cyan]Available Commands[/]")
            .AddColumn(new TableColumn("[bold]Command[/]").Width(16))
            .AddColumn(new TableColumn("[bold]Description[/]"));

        foreach (var cmd in router.All)
        {
            var aliases = cmd.Aliases.Length > 0
                ? $" [dim]({string.Join(", ", cmd.Aliases.Select(a => "/" + a))})[/]"
                : "";
            table.AddRow($"[cyan]/{Markup.Escape(cmd.Name)}[/]{aliases}", cmd.Description);
        }

        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}
