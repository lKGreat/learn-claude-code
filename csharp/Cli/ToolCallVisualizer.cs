using System.Diagnostics;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Intercepts SK function (tool) invocations to display real-time visualization.
///
/// Uses IAutoFunctionInvocationFilter to hook into the tool call pipeline:
///   Before: show tool name + key arguments with a spinner
///   After:  show result summary in a panel with elapsed time
///
/// Also tracks total tool call count in CliContext.
/// </summary>
public class ToolCallVisualizer : IAutoFunctionInvocationFilter
{
    private readonly CliContext? _context;

    public ToolCallVisualizer(CliContext? context = null)
    {
        _context = context;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var arguments = context.Arguments;

        // Extract a readable summary of key arguments
        var argSummary = GetArgumentSummary(functionName, arguments);

        // Show the tool call header
        var header = string.IsNullOrEmpty(argSummary)
            ? $"[bold cyan]{Markup.Escape(functionName)}[/]"
            : $"[bold cyan]{Markup.Escape(functionName)}[/] [dim]{Markup.Escape(argSummary)}[/]";

        AnsiConsole.MarkupLine($"  [dim]──[/] {header}");

        var sw = Stopwatch.StartNew();

        // Execute the actual tool
        await next(context);

        sw.Stop();

        // Show result summary
        var result = context.Result;
        var resultText = result?.ToString() ?? "";

        // Truncate long results for display
        var displayResult = TruncateResult(resultText, maxLines: 8, maxChars: 500);

        if (!string.IsNullOrWhiteSpace(displayResult))
        {
            var panel = new Panel(Markup.Escape(displayResult))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Header = new PanelHeader($"[dim] result ({sw.Elapsed.TotalSeconds:F1}s) [/]"),
                Padding = new Padding(1, 0),
            };
            AnsiConsole.Write(new Padder(panel, new Padding(4, 0, 0, 0)));
        }
        else
        {
            AnsiConsole.MarkupLine($"     [dim]done ({sw.Elapsed.TotalSeconds:F1}s)[/]");
        }

        // Track tool calls
        if (_context != null)
            _context.TotalToolCalls++;
    }

    /// <summary>
    /// Extract a readable one-liner from the function arguments.
    /// </summary>
    private static string GetArgumentSummary(string functionName, KernelArguments? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "";

        // Show specific arg based on function name for better UX
        return functionName switch
        {
            "bash" => GetArg(arguments, "command"),
            "read_file" => GetArg(arguments, "path"),
            "write_file" => GetArg(arguments, "path"),
            "edit_file" => GetArg(arguments, "path"),
            "grep" => GetArg(arguments, "pattern"),
            "glob" => GetArg(arguments, "pattern"),
            "list_directory" => GetArg(arguments, "path"),
            "web_search" => GetArg(arguments, "query"),
            "web_fetch" => GetArg(arguments, "url"),
            "Task" => GetArg(arguments, "description"),
            _ => SummarizeArgs(arguments),
        };
    }

    private static string GetArg(KernelArguments args, string key)
    {
        if (args.TryGetValue(key, out var val) && val != null)
        {
            var s = val.ToString() ?? "";
            return s.Length > 80 ? s[..77] + "..." : s;
        }
        return "";
    }

    private static string SummarizeArgs(KernelArguments args)
    {
        var parts = args
            .Where(kv => kv.Value != null)
            .Take(2)
            .Select(kv =>
            {
                var v = kv.Value?.ToString() ?? "";
                if (v.Length > 40) v = v[..37] + "...";
                return $"{kv.Key}={v}";
            });
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Truncate result text for display: limit lines and total characters.
    /// </summary>
    private static string TruncateResult(string text, int maxLines, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var lines = text.Split('\n');
        if (lines.Length > maxLines)
        {
            var taken = string.Join('\n', lines.Take(maxLines));
            return taken.Length > maxChars
                ? taken[..maxChars] + $"\n... ({lines.Length - maxLines} more lines)"
                : taken + $"\n... ({lines.Length - maxLines} more lines)";
        }

        return text.Length > maxChars
            ? text[..maxChars] + "..."
            : text;
    }
}
