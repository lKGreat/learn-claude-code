using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class CostCommand : ISlashCommand
{
    public string Name => "cost";
    public string Description => "Show token usage and cost estimate";

    public Task ExecuteAsync(string args, CliContext context)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Title("[bold]Usage[/]")
            .AddColumn(new TableColumn("[bold]Metric[/]").Width(16))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        table.AddRow("Provider", Markup.Escape(context.ActiveConfig.DisplayName));
        table.AddRow("Turns", context.TurnCount.ToString());
        table.AddRow("Tool Calls", context.TotalToolCalls.ToString());
        table.AddRow("[dim]Note[/]", "[dim]Detailed token tracking coming in Phase 4[/]");

        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}
