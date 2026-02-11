using MiniClaudeCode.Services;
using MiniClaudeCode.Services.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class ModelCommand : ISlashCommand
{
    public string Name => "model";
    public string Description => "Switch the active model";

    public Task ExecuteAsync(string args, CliContext context)
    {
        // If a model ID was given directly, try to apply it
        if (!string.IsNullOrWhiteSpace(args))
        {
            AnsiConsole.MarkupLine($"[yellow]To change provider, use /provider. Model ID override is not yet supported mid-session.[/]");
            return Task.CompletedTask;
        }

        // Show current model and available providers
        AnsiConsole.MarkupLine($"[dim]Current:[/] [bold]{Markup.Escape(context.ActiveConfig.DisplayName)}[/]");
        AnsiConsole.WriteLine();

        // List all available providers with their models
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Provider")
            .AddColumn("Model ID")
            .AddColumn("Status");

        foreach (var (provider, config) in context.ProviderConfigs)
        {
            var isCurrent = provider == context.ActiveConfig.Provider;
            var status = isCurrent ? "[green]active[/]" : "[dim]available[/]";
            table.AddRow(
                Markup.Escape(provider.ToString()),
                Markup.Escape(config.ModelId),
                status);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[dim]Use /provider to switch the active provider.[/]");
        return Task.CompletedTask;
    }
}
