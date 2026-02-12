using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Adapters;

/// <summary>
/// Implements IOutputSink by pushing messages to the ChatViewModel.
/// All calls are marshalled to the UI thread.
/// </summary>
public class AvaloniaOutputSink(ChatViewModel chat) : IOutputSink
{
    public void WriteAssistant(string content)
        => DispatcherService.Post(() => chat.AddAssistantMessage(content));

    public void WriteSystem(string message)
        => DispatcherService.Post(() => chat.AddSystemMessage(message));

    public void WriteError(string message)
        => DispatcherService.Post(() => chat.AddErrorMessage(message));

    public void WriteWarning(string message)
        => DispatcherService.Post(() => chat.AddWarningMessage(message));

    public void WriteDebug(string message)
        => DispatcherService.Post(() => chat.AddSystemMessage($"[DEBUG] {message}"));

    public void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
            DispatcherService.Post(() => chat.AddSystemMessage(text));
    }

    public void Clear()
        => DispatcherService.Post(() => chat.ClearMessages());

    // =========================================================================
    // Streaming methods for real-time token-by-token display
    // =========================================================================

    public void StreamStart(string messageId)
        => DispatcherService.Post(() => chat.BeginStreamingMessage());

    public void StreamAppend(string messageId, string chunk)
        => DispatcherService.Post(() => chat.AppendToStreaming(chunk));

    public void StreamEnd(string messageId)
        => DispatcherService.Post(() => chat.EndStreaming());
}
