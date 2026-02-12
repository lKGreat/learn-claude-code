namespace MiniClaudeCode.Abstractions.UI;

/// <summary>
/// Abstraction for streaming output from agents to any UI.
/// Replaces all direct Console.Write/AnsiConsole calls in core logic.
/// </summary>
public interface IOutputSink
{
    /// <summary>Write an assistant (LLM) response message.</summary>
    void WriteAssistant(string content);

    /// <summary>Write a system/info message.</summary>
    void WriteSystem(string message);

    /// <summary>Write an error message.</summary>
    void WriteError(string message);

    /// <summary>Write a warning message.</summary>
    void WriteWarning(string message);

    /// <summary>Write a debug/trace message (may be hidden in some UIs).</summary>
    void WriteDebug(string message);

    /// <summary>Write a raw line of text.</summary>
    void WriteLine(string text = "");

    /// <summary>Clear the output area.</summary>
    void Clear();
}
