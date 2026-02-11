using MiniClaudeCode.Services;
using MiniClaudeCode.Services.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class ProviderCommand : ISlashCommand
{
    public string Name => "provider";
    public string Description => "Switch the active model provider";

    public Task ExecuteAsync(string args, CliContext context)
    {
        if (context.ProviderConfigs.Count <= 1)
        {
            AnsiConsole.MarkupLine("[yellow]Only one provider is configured. Add more API keys in .env to enable switching.[/]");
            return Task.CompletedTask;
        }

        // Build choices
        var choices = context.ProviderConfigs.Values
            .Select(c => c.DisplayName)
            .ToList();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select provider:[/]")
                .AddChoices(choices)
                .HighlightStyle(new Style(Color.Cyan1)));

        // Find the matching config
        var newConfig = context.ProviderConfigs.Values.First(c => c.DisplayName == selected);

        if (newConfig.Provider == context.ActiveConfig.Provider)
        {
            AnsiConsole.MarkupLine($"[dim]Already using {Markup.Escape(newConfig.DisplayName)}.[/]");
            return Task.CompletedTask;
        }

        // Rebuild kernel with new provider
        var builder = Kernel.CreateBuilder();
        switch (newConfig.Provider)
        {
            case ModelProvider.DeepSeek:
                builder.AddDeepSeekChatCompletion(newConfig.ApiKey, newConfig.ModelId);
                break;
            case ModelProvider.Zhipu:
                builder.AddZhipuChatCompletion(newConfig.ApiKey, newConfig.ModelId);
                break;
            case ModelProvider.OpenAI:
            default:
                if (!string.IsNullOrEmpty(newConfig.BaseUrl))
                    builder.AddOpenAIChatCompletion(newConfig.ModelId, new Uri(newConfig.BaseUrl), newConfig.ApiKey);
                else
                    builder.AddOpenAIChatCompletion(newConfig.ModelId, newConfig.ApiKey);
                break;
        }

        // Re-register all existing plugins
        foreach (var plugin in context.Kernel.Plugins)
            builder.Plugins.Add(plugin);

        var newKernel = builder.Build();
        context.Kernel = newKernel;
        context.ActiveConfig = newConfig;

        // Rebuild agent with new kernel
        context.Agent = new ChatCompletionAgent
        {
            Name = "MiniClaudeCode",
            Instructions = context.Agent.Instructions,
            Kernel = newKernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        AnsiConsole.MarkupLine($"[green]Switched to {Markup.Escape(newConfig.DisplayName)}[/]");
        return Task.CompletedTask;
    }
}
