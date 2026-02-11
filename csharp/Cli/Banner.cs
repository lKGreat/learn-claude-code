using MiniClaudeCode.Services;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Renders the branded startup banner using Spectre.Console.
/// </summary>
public static class Banner
{
    public static void Render(CliContext ctx)
    {
        // Title panel
        var title = new Panel(
            Align.Center(new Markup(
                $"[bold cyan]Mini Claude Code[/]  [dim]v{CliArgs.Version}[/]\n" +
                "[dim]── C# Semantic Kernel Edition ──[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 0, 2, 0),
        };
        AnsiConsole.Write(title);
        AnsiConsole.WriteLine();

        // Info grid
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(12).NoWrap());
        grid.AddColumn(new GridColumn());

        grid.AddRow("[dim]Provider[/]", $"[bold]{Markup.Escape(ctx.ActiveConfig.DisplayName)}[/]");
        grid.AddRow("[dim]Workspace[/]", $"[blue]{Markup.Escape(ctx.WorkDir)}[/]");

        // Tools count
        var toolCount = ctx.Kernel.Plugins.SelectMany(p => p).Count();
        var pluginCount = ctx.Kernel.Plugins.Count;
        grid.AddRow("[dim]Tools[/]", $"{toolCount} tools across {pluginCount} plugins");

        // Skills
        var skills = ctx.SkillLoader.ListSkills();
        grid.AddRow("[dim]Skills[/]", skills.Count > 0
            ? string.Join(", ", skills)
            : "[dim]none[/]");

        // Rules
        var rules = ctx.RulesLoader.ListRules();
        var hasAlwaysApply = ctx.RulesLoader.GetAlwaysApplyRulesContent().Length > 0;
        grid.AddRow("[dim]Rules[/]", rules.Count > 0
            ? $"{rules.Count} ({(hasAlwaysApply ? "with always-apply" : "on-demand")})"
            : "[dim]none[/]");

        // Agents
        var agentTypes = SubAgentRunner.GetAgentTypeNames();
        grid.AddRow("[dim]Agents[/]", string.Join(", ", agentTypes));

        // Agent overrides
        if (ctx.AgentProviderOverrides.Count > 0)
        {
            var overrides = string.Join(", ", ctx.AgentProviderOverrides.Select(
                kv => $"{kv.Key}={ctx.ProviderConfigs[kv.Value].DisplayName}"));
            grid.AddRow("[dim]Overrides[/]", $"[yellow]{Markup.Escape(overrides)}[/]");
        }

        AnsiConsole.Write(new Padder(grid, new Padding(2, 0, 0, 0)));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]Type[/] [cyan]/help[/] [dim]for commands,[/] [cyan]/exit[/] [dim]to quit.[/]");
        AnsiConsole.WriteLine();
    }
}
