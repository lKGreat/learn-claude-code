using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.Services;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// TodoWrite tool - enables structured planning and progress tracking.
/// </summary>
public class TodoPlugin
{
    private readonly ITodoManager _manager;
    private readonly IOutputSink _output;

    public TodoPlugin(ITodoManager manager, IOutputSink output)
    {
        _manager = manager;
        _output = output;
    }

    [KernelFunction("TodoWrite")]
    [Description(@"Update the task list. Use to plan and track progress on multi-step tasks.
Pass a JSON array of items, each with: content (string), status (pending|in_progress|completed), activeForm (present tense action).
Only ONE item can be in_progress at a time. Max 20 items.")]
    public string UpdateTodos(
        [Description("JSON array of todo items")] string itemsJson)
    {
        try
        {
            var result = _manager.Update(itemsJson);
            _output.WriteDebug(result);
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
