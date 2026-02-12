namespace MiniClaudeCode.Abstractions.Services;

/// <summary>
/// Manages a structured task list for agent planning and progress tracking.
/// </summary>
public interface ITodoManager
{
    /// <summary>
    /// Validate and update the todo list from a JSON array.
    /// Returns rendered text view.
    /// </summary>
    string Update(string itemsJson);

    /// <summary>
    /// Render the current todo list as human-readable text.
    /// </summary>
    string Render();
}
