using System.Text;
using System.Text.Json;
using MiniClaudeCode.Abstractions.Services;

namespace MiniClaudeCode.Core.Services;

/// <summary>
/// Manages a structured task list with enforced constraints.
/// </summary>
public class TodoManager : ITodoManager
{
    private List<TodoItem> _items = [];

    public record TodoItem(string Content, string Status, string ActiveForm);

    public string Update(string itemsJson)
    {
        List<TodoItemInput>? inputs;
        try
        {
            inputs = JsonSerializer.Deserialize<List<TodoItemInput>>(itemsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON: {ex.Message}");
        }

        if (inputs is null || inputs.Count == 0)
            throw new ArgumentException("Items array is empty");

        if (inputs.Count > 20)
            throw new ArgumentException("Max 20 todos allowed");

        var validated = new List<TodoItem>();
        int inProgressCount = 0;

        for (int i = 0; i < inputs.Count; i++)
        {
            var item = inputs[i];

            if (string.IsNullOrWhiteSpace(item.Content))
                throw new ArgumentException($"Item {i}: content required");

            var status = (item.Status ?? "pending").ToLowerInvariant();
            if (status is not ("pending" or "in_progress" or "completed"))
                throw new ArgumentException($"Item {i}: invalid status '{status}'");

            if (string.IsNullOrWhiteSpace(item.ActiveForm))
                throw new ArgumentException($"Item {i}: activeForm required");

            if (status == "in_progress")
                inProgressCount++;

            validated.Add(new TodoItem(item.Content.Trim(), status, item.ActiveForm.Trim()));
        }

        if (inProgressCount > 1)
            throw new ArgumentException("Only one task can be in_progress at a time");

        _items = validated;
        return Render();
    }

    public string Render()
    {
        if (_items.Count == 0)
            return "No todos.";

        var sb = new StringBuilder();
        foreach (var item in _items)
        {
            var mark = item.Status switch
            {
                "completed" => "[x]",
                "in_progress" => "[>]",
                _ => "[ ]"
            };

            sb.Append($"{mark} {item.Content}");
            if (item.Status == "in_progress")
                sb.Append($" <- {item.ActiveForm}");
            sb.AppendLine();
        }

        int completed = _items.Count(t => t.Status == "completed");
        sb.AppendLine($"\n({completed}/{_items.Count} completed)");

        return sb.ToString();
    }

    private record TodoItemInput
    {
        public string? Content { get; init; }
        public string? Status { get; init; }
        public string? ActiveForm { get; init; }
    }
}
