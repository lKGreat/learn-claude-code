using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Services;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// TodoWrite tool - enables structured planning and progress tracking.
///
/// The model sends a complete task list (not a diff) as JSON.
/// We validate constraints and return a rendered text view.
///
/// This is the key addition that enables complex multi-step task completion.
/// Without visible plans, the model "forgets" what it was doing after ~10 tool calls.
/// </summary>
public class TodoPlugin
{
    private readonly TodoManager _manager;

    public TodoPlugin(TodoManager manager)
    {
        _manager = manager;
    }

    [KernelFunction("TodoWrite")]
    [Description(@"Update the task list. Use to plan and track progress on multi-step tasks.
Pass a JSON array of items, each with: content (string), status (pending|in_progress|completed), activeForm (present tense action).
Only ONE item can be in_progress at a time. Max 20 items.
Example: [{""content"":""Add tests"",""status"":""in_progress"",""activeForm"":""Writing unit tests""}]")]
    public string UpdateTodos(
        [Description("JSON array of todo items")] string itemsJson)
    {
        try
        {
            var result = _manager.Update(itemsJson);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(result);
            Console.ResetColor();
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
