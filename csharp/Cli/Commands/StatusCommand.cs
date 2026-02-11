using MiniClaudeCode.Services;
using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class StatusCommand : ISlashCommand
{
    public string Name => "status";
    public string[] Aliases => ["info"];
    public string Description => "Show session status and configuration";

    public Task ExecuteAsync(string args, CliContext context)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold]Session Status[/]")
            .AddColumn(new TableColumn("[bold]Property[/]").Width(16))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        table.AddRow("Provider", Markup.Escape(context.ActiveConfig.DisplayName));
        table.AddRow("Workspace", Markup.Escape(context.WorkDir));
        table.AddRow("Turns", context.TurnCount.ToString());
        table.AddRow("Tool Calls", context.TotalToolCalls.ToString());

        var toolCount = context.Kernel.Plugins.SelectMany(p => p).Count();
        table.AddRow("Tools", $"{toolCount} across {context.Kernel.Plugins.Count} plugins");

        var agents = string.Join(", ", SubAgentRunner.GetAgentTypeNames());
        table.AddRow("Agent Types", agents);

        if (context.AgentProviderOverrides.Count > 0)
        {
            var overrides = string.Join(", ", context.AgentProviderOverrides.Select(
                kv => $"{kv.Key}={context.ProviderConfigs[kv.Value].DisplayName}"));
            table.AddRow("Agent Overrides", Markup.Escape(overrides));
        }

        // Available providers
        var providers = string.Join(", ", context.ProviderConfigs.Values.Select(c => c.DisplayName));
        table.AddRow("All Providers", Markup.Escape(providers));

        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}
