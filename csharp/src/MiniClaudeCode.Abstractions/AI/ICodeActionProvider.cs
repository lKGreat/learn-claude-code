namespace MiniClaudeCode.Abstractions.AI;

/// <summary>
/// Provides AI-powered code actions (quick fixes, refactorings, etc.) for the editor.
/// Displayed in the lightbulb menu when invoked on a code selection or diagnostic.
/// </summary>
public interface ICodeActionProvider
{
    /// <summary>
    /// Retrieves available code actions for the given context.
    /// Actions are displayed in the editor's quick fix / refactor menu.
    /// </summary>
    /// <param name="context">Context including file path, selection, and any diagnostic messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available code actions.</returns>
    Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        CodeActionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information for code action requests.
/// </summary>
public record CodeActionContext
{
    /// <summary>Absolute path to the file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Language identifier.</summary>
    public required string Language { get; init; }

    /// <summary>The selected range in the editor.</summary>
    public required TextRange SelectionRange { get; init; }

    /// <summary>Full content of the file.</summary>
    public required string FileContent { get; init; }

    /// <summary>Diagnostic message if this action is triggered by an error/warning.</summary>
    public string? DiagnosticMessage { get; init; }

    /// <summary>Severity of the diagnostic (if applicable).</summary>
    public DiagnosticSeverity? DiagnosticSeverity { get; init; }
}

/// <summary>
/// Represents a single code action that can be performed.
/// </summary>
public record CodeAction
{
    /// <summary>Human-readable title displayed in the UI.</summary>
    public required string Title { get; init; }

    /// <summary>The kind of code action (quick fix, refactor, extract, etc.).</summary>
    public required CodeActionKind Kind { get; init; }

    /// <summary>The handler that executes this action.</summary>
    public required Func<CancellationToken, Task<EditResult>> ExecuteAsync { get; init; }

    /// <summary>Optional tooltip/description for the action.</summary>
    public string? Description { get; init; }

    /// <summary>True if this action is preferred (should be highlighted in UI).</summary>
    public bool IsPreferred { get; init; }
}

/// <summary>
/// Kind of code action, following LSP conventions.
/// </summary>
public enum CodeActionKind
{
    /// <summary>Quick fix for a diagnostic.</summary>
    QuickFix,

    /// <summary>General refactoring.</summary>
    Refactor,

    /// <summary>Extract to method/variable/constant.</summary>
    RefactorExtract,

    /// <summary>Inline method/variable.</summary>
    RefactorInline,

    /// <summary>Rewrite code for clarity or modernization.</summary>
    RefactorRewrite,

    /// <summary>Source code organization (imports, formatting, etc.).</summary>
    Source,

    /// <summary>Source action specifically for organizing imports.</summary>
    SourceOrganizeImports
}

/// <summary>
/// Severity level for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Error that prevents compilation.</summary>
    Error,

    /// <summary>Warning that should be addressed.</summary>
    Warning,

    /// <summary>Informational message.</summary>
    Information,

    /// <summary>Hint for code improvement.</summary>
    Hint
}

/// <summary>
/// Represents a range of text in a document.
/// </summary>
public record TextRange
{
    /// <summary>Zero-based start line.</summary>
    public required int StartLine { get; init; }

    /// <summary>Zero-based start column.</summary>
    public required int StartColumn { get; init; }

    /// <summary>Zero-based end line.</summary>
    public required int EndLine { get; init; }

    /// <summary>Zero-based end column.</summary>
    public required int EndColumn { get; init; }
}
