namespace MiniClaudeCode.Abstractions.AI;

/// <summary>
/// Builds rich context bundles from the codebase for various AI operations.
/// Responsible for gathering relevant code snippets, file metadata, and symbol information.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Builds a lightweight context bundle for inline code completion.
    /// Focuses on immediate surrounding code and related symbols.
    /// </summary>
    /// <param name="filePath">Absolute path to the file being edited.</param>
    /// <param name="cursorLine">Zero-based line number of the cursor.</param>
    /// <param name="cursorColumn">Zero-based column number of the cursor.</param>
    /// <param name="maxTokens">Maximum token budget for the context bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A context bundle optimized for completion.</returns>
    Task<ContextBundle> BuildCompletionContextAsync(
        string filePath,
        int cursorLine,
        int cursorColumn,
        int maxTokens = 2000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a comprehensive context bundle for chat interactions.
    /// Includes @ mentioned files, symbols, and broader workspace context.
    /// </summary>
    /// <param name="filePaths">Explicitly mentioned file paths.</param>
    /// <param name="symbolNames">Explicitly mentioned symbol names (classes, methods, etc.).</param>
    /// <param name="query">The user's chat query for semantic search.</param>
    /// <param name="maxTokens">Maximum token budget for the context bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A comprehensive context bundle for chat.</returns>
    Task<ContextBundle> BuildChatContextAsync(
        IReadOnlyList<string>? filePaths = null,
        IReadOnlyList<string>? symbolNames = null,
        string? query = null,
        int maxTokens = 8000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a focused context bundle for inline edit operations (Cmd+K style).
    /// Includes the selected code, surrounding context, and related definitions.
    /// </summary>
    /// <param name="filePath">Absolute path to the file being edited.</param>
    /// <param name="startLine">Zero-based start line of the selection.</param>
    /// <param name="endLine">Zero-based end line of the selection.</param>
    /// <param name="instruction">The natural language edit instruction.</param>
    /// <param name="maxTokens">Maximum token budget for the context bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A context bundle optimized for inline edits.</returns>
    Task<ContextBundle> BuildEditContextAsync(
        string filePath,
        int startLine,
        int endLine,
        string instruction,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default);
}
