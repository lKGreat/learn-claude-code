namespace MiniClaudeCode.Abstractions.UI;

/// <summary>
/// Abstraction for user interaction, replacing direct Console.ReadLine/Write calls.
/// Each UI frontend (TUI, WinForms, Web) provides its own implementation.
/// </summary>
public interface IUserInteraction
{
    /// <summary>
    /// Ask the user a question and wait for their response.
    /// </summary>
    /// <param name="question">The question to display.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's answer, or null if cancelled/no answer.</returns>
    Task<string?> AskAsync(string question, CancellationToken ct = default);

    /// <summary>
    /// Ask the user to select from a list of choices.
    /// </summary>
    /// <param name="title">Prompt title.</param>
    /// <param name="choices">Available choices.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Selected choice, or null if cancelled.</returns>
    Task<string?> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken ct = default);

    /// <summary>
    /// Ask the user for a secret (e.g. API key) without echoing.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret string, or null if cancelled.</returns>
    Task<string?> AskSecretAsync(string prompt, CancellationToken ct = default);

    /// <summary>
    /// Show a confirmation dialog (yes/no).
    /// </summary>
    Task<bool> ConfirmAsync(string message, CancellationToken ct = default);
}
