namespace MiniClaudeCode.Abstractions.AI;

/// <summary>
/// Input context for inline AI code completion.
/// Contains all information needed to generate a completion at the current cursor position.
/// </summary>
public record CompletionContext
{
    /// <summary>Absolute path to the file being edited.</summary>
    public required string FilePath { get; init; }

    /// <summary>Language identifier (e.g., "csharp", "typescript", "python").</summary>
    public required string Language { get; init; }

    /// <summary>Text content before the cursor position.</summary>
    public required string TextBeforeCursor { get; init; }

    /// <summary>Text content after the cursor position.</summary>
    public required string TextAfterCursor { get; init; }

    /// <summary>Zero-based line number of the cursor.</summary>
    public required int CursorLine { get; init; }

    /// <summary>Zero-based column number of the cursor.</summary>
    public required int CursorColumn { get; init; }

    /// <summary>Absolute paths to all currently open files in the editor for additional context.</summary>
    public IReadOnlyList<string>? OpenFilePaths { get; init; }
}

/// <summary>
/// Result of an inline AI code completion request.
/// </summary>
public record CompletionResult
{
    /// <summary>The completion text to insert at the cursor position, or null if no completion available.</summary>
    public string? Text { get; init; }

    /// <summary>Confidence score (0.0 to 1.0) indicating the quality of the completion.</summary>
    public double Confidence { get; init; }

    /// <summary>Name or identifier of the AI model that generated this completion.</summary>
    public string? ModelUsed { get; init; }

    /// <summary>True if this is a multi-line completion, false for single-line.</summary>
    public bool IsMultiLine { get; init; }
}

/// <summary>
/// Input context for inline code editing (Cmd+K / Ctrl+K style edits).
/// </summary>
public record EditContext
{
    /// <summary>Absolute path to the file being edited.</summary>
    public required string FilePath { get; init; }

    /// <summary>Language identifier (e.g., "csharp", "typescript", "python").</summary>
    public required string Language { get; init; }

    /// <summary>The currently selected code that will be edited/replaced.</summary>
    public required string SelectedCode { get; init; }

    /// <summary>Natural language instruction describing the desired edit.</summary>
    public required string Instruction { get; init; }

    /// <summary>Zero-based start line of the selection.</summary>
    public required int SelectionStartLine { get; init; }

    /// <summary>Zero-based end line of the selection.</summary>
    public required int SelectionEndLine { get; init; }

    /// <summary>Full file content before the edit for context.</summary>
    public string? FullFileContent { get; init; }

    /// <summary>Additional context snippets from other files.</summary>
    public IReadOnlyList<string>? AdditionalContext { get; init; }
}

/// <summary>
/// Result of an inline edit operation.
/// </summary>
public record EditResult
{
    /// <summary>The original code that was edited.</summary>
    public required string OriginalCode { get; init; }

    /// <summary>The modified code after applying the edit.</summary>
    public required string ModifiedCode { get; init; }

    /// <summary>Unified diff representation of the changes.</summary>
    public string? Diff { get; init; }

    /// <summary>Explanation of the changes made.</summary>
    public string? Explanation { get; init; }

    /// <summary>True if the edit was successful, false otherwise.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if Success is false.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Context bundle containing relevant code snippets and metadata for AI operations.
/// </summary>
public record ContextBundle
{
    /// <summary>List of relevant code snippets from the codebase.</summary>
    public required IReadOnlyList<CodeSnippet> Snippets { get; init; }

    /// <summary>Estimated token count for the entire context bundle.</summary>
    public int EstimatedTokenCount { get; init; }

    /// <summary>Additional metadata about the context.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// A code snippet included in a context bundle.
/// </summary>
public record CodeSnippet
{
    /// <summary>Absolute path to the source file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Language identifier.</summary>
    public required string Language { get; init; }

    /// <summary>The code content.</summary>
    public required string Content { get; init; }

    /// <summary>Zero-based start line of the snippet.</summary>
    public int StartLine { get; init; }

    /// <summary>Zero-based end line of the snippet.</summary>
    public int EndLine { get; init; }

    /// <summary>Relevance score (0.0 to 1.0) indicating how relevant this snippet is to the current context.</summary>
    public double RelevanceScore { get; init; }
}
