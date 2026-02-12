using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the chat panel - manages messages and input.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    private readonly List<string> _inputHistory = [];
    private int _historyIndex = -1;

    // Streaming state
    private ChatMessage? _streamingMessage;

    /// <summary>
    /// Fired when the user submits a message.
    /// </summary>
    public event Action<string>? MessageSubmitted;

    /// <summary>
    /// Fired when messages change to trigger auto-scroll.
    /// </summary>
    public event Action? ScrollToBottomRequested;

    [RelayCommand]
    private void SendMessage()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrEmpty(text) || IsProcessing) return;

        // Add to history
        _inputHistory.Add(text);
        _historyIndex = _inputHistory.Count;

        InputText = string.Empty;
        MessageSubmitted?.Invoke(text);
    }

    public void NavigateHistoryUp()
    {
        if (_inputHistory.Count == 0) return;
        if (_historyIndex > 0)
        {
            _historyIndex--;
            InputText = _inputHistory[_historyIndex];
        }
    }

    public void NavigateHistoryDown()
    {
        if (_inputHistory.Count == 0) return;
        if (_historyIndex < _inputHistory.Count - 1)
        {
            _historyIndex++;
            InputText = _inputHistory[_historyIndex];
        }
        else
        {
            _historyIndex = _inputHistory.Count;
            InputText = string.Empty;
        }
    }

    // =========================================================================
    // Standard message methods
    // =========================================================================

    public void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = content });
        ScrollToBottomRequested?.Invoke();
    }

    public void AddAssistantMessage(string content)
    {
        var msg = new ChatMessage { Role = ChatMessageRole.Assistant, Content = content };
        // Auto-collapse long assistant messages
        if (msg.IsCollapsible)
            msg.IsExpanded = false;
        Messages.Add(msg);
        ScrollToBottomRequested?.Invoke();
    }

    public void AddSystemMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatMessageRole.System, Content = content });
        ScrollToBottomRequested?.Invoke();
    }

    public void AddErrorMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatMessageRole.Error, Content = content });
        ScrollToBottomRequested?.Invoke();
    }

    public void AddWarningMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatMessageRole.Warning, Content = content });
        ScrollToBottomRequested?.Invoke();
    }

    // =========================================================================
    // Streaming methods for real-time token-by-token display
    // =========================================================================

    /// <summary>
    /// Begin a streaming assistant message. Returns the placeholder message.
    /// </summary>
    public ChatMessage BeginStreamingMessage()
    {
        _streamingMessage = new ChatMessage
        {
            Role = ChatMessageRole.Assistant,
            IsStreaming = true,
            Content = ""
        };
        Messages.Add(_streamingMessage);
        ScrollToBottomRequested?.Invoke();
        return _streamingMessage;
    }

    /// <summary>
    /// Append a text chunk to the current streaming message.
    /// </summary>
    public void AppendToStreaming(string chunk)
    {
        if (_streamingMessage != null)
        {
            _streamingMessage.Content += chunk;
            ScrollToBottomRequested?.Invoke();
        }
    }

    /// <summary>
    /// Update the streaming elapsed time display.
    /// </summary>
    public void UpdateStreamingElapsed(TimeSpan elapsed)
    {
        if (_streamingMessage != null)
        {
            _streamingMessage.StreamingElapsed = $"{elapsed.TotalSeconds:F1}s";
        }
    }

    /// <summary>
    /// Finalize the streaming message.
    /// </summary>
    public void EndStreaming(TimeSpan? finalElapsed = null)
    {
        if (_streamingMessage != null)
        {
            _streamingMessage.IsStreaming = false;
            if (finalElapsed.HasValue)
                _streamingMessage.StreamingElapsed = $"{finalElapsed.Value.TotalSeconds:F1}s";
            else
                _streamingMessage.StreamingElapsed = "";
            _streamingMessage = null;
        }
    }

    public void ClearMessages()
    {
        _streamingMessage = null;
        Messages.Clear();
    }
}
