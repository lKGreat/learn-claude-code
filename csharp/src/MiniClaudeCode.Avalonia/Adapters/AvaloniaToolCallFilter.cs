using System.Diagnostics;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Avalonia.Adapters;

/// <summary>
/// Semantic Kernel auto function invocation filter that reports tool calls to the observer.
/// Mirrors TuiToolCallFilter from the TUI project.
/// </summary>
public class AvaloniaToolCallFilter(IToolCallObserver observer, EngineContext engine)
    : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var argSummary = GetArgumentSummary(functionName, context.Arguments);

        var toolCall = new ToolCallEvent
        {
            FunctionName = functionName,
            PluginName = context.Function.PluginName,
            ArgumentSummary = argSummary,
        };

        observer.OnToolCallStarted(toolCall);

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
            sw.Stop();

            toolCall.Elapsed = sw.Elapsed;
            toolCall.Result = context.Result?.ToString();
            toolCall.Success = true;

            observer.OnToolCallCompleted(toolCall);
        }
        catch (Exception ex)
        {
            sw.Stop();
            toolCall.Elapsed = sw.Elapsed;
            toolCall.Result = ex.Message;
            toolCall.Success = false;

            observer.OnToolCallFailed(toolCall, ex.Message);
            throw;
        }

        engine.TotalToolCalls++;
    }

    private static string GetArgumentSummary(string functionName, KernelArguments? arguments)
    {
        if (arguments == null || arguments.Count == 0) return "";

        return functionName switch
        {
            "bash" => GetArg(arguments, "command"),
            "read_file" or "write_file" or "edit_file" => GetArg(arguments, "path"),
            "grep" or "glob" => GetArg(arguments, "pattern"),
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
        var parts = args.Where(kv => kv.Value != null).Take(2)
            .Select(kv =>
            {
                var v = kv.Value?.ToString() ?? "";
                if (v.Length > 40) v = v[..37] + "...";
                return $"{kv.Key}={v}";
            });
        return string.Join(", ", parts);
    }
}
