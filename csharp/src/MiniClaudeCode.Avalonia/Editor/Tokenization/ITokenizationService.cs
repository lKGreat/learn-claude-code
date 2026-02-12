namespace MiniClaudeCode.Avalonia.Editor.Tokenization;

/// <summary>
/// Service interface for managing TextMate grammar loading and language detection.
/// Abstracts the TextMateSharp integration for syntax highlighting.
/// </summary>
public interface ITokenizationService
{
    /// <summary>Get the TextMate scope name for a language id.</summary>
    string? GetScopeName(string languageId);

    /// <summary>Get a language id from a file extension.</summary>
    string GetLanguageId(string fileExtension);

    /// <summary>Whether tokenization should be disabled for large files.</summary>
    bool ShouldDisableTokenization(long fileSize, int lineCount);

    /// <summary>Get all registered language ids.</summary>
    IReadOnlyList<string> RegisteredLanguages { get; }
}
