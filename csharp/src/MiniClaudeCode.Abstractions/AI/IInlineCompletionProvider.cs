namespace MiniClaudeCode.Abstractions.AI;

/// <summary>
/// Provides inline AI-powered code completions at the cursor position.
/// This is the core interface for features like GitHub Copilot-style inline suggestions.
/// </summary>
public interface IInlineCompletionProvider
{
    /// <summary>
    /// Requests an inline code completion based on the current editor context.
    /// Returns null if no suitable completion is available.
    /// </summary>
    /// <param name="context">Context information including cursor position, surrounding code, and file metadata.</param>
    /// <param name="cancellationToken">Cancellation token to abort the request.</param>
    /// <returns>A completion result, or null if no completion is available.</returns>
    Task<CompletionResult?> GetCompletionAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default);
}
