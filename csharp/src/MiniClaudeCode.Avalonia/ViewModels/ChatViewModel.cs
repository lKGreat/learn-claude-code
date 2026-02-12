using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Abstractions.Indexing;
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

    /// <summary>
    /// Mention picker for selecting files/symbols with @.
    /// </summary>
    public MentionPickerViewModel MentionPicker { get; } = new();

    /// <summary>
    /// List of file paths mentioned in the current message with @.
    /// </summary>
    public ObservableCollection<string> MentionedFiles { get; } = [];

    private readonly List<string> _inputHistory = [];
    private int _historyIndex = -1;

    // Streaming state
    private ChatMessage? _streamingMessage;

    // Mention tracking
    private int _lastAtSymbolPosition = -1;

    public ChatViewModel()
    {
        // Wire up mention picker events
        MentionPicker.MentionSelected += OnMentionSelected;
        MentionPicker.Dismissed += OnMentionPickerDismissed;
    }

    /// <summary>
    /// Fired when the user submits a message.
    /// </summary>
    public event Action<string>? MessageSubmitted;

    /// <summary>
    /// Fired when messages change to trigger auto-scroll.
    /// </summary>
    public event Action? ScrollToBottomRequested;

    /// <summary>
    /// Set the indexing services for mention picker.
    /// </summary>
    public void SetIndexServices(ICodebaseIndex codebaseIndex, ISymbolIndex symbolIndex)
    {
        MentionPicker.SetIndexServices(codebaseIndex, symbolIndex);
    }

    /// <summary>
    /// Get the file contents for all mentioned files to inject into AI context.
    /// </summary>
    public async Task<string> GetMentionContextAsync()
    {
        if (MentionedFiles.Count == 0)
            return string.Empty;

        var context = new System.Text.StringBuilder();
        context.AppendLine("## Mentioned Files Context\n");

        foreach (var filePath in MentionedFiles)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);
                    context.AppendLine($"### {relativePath}");
                    context.AppendLine("```");
                    context.AppendLine(content);
                    context.AppendLine("```\n");
                }
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return context.ToString();
    }

    /// <summary>
    /// Handle input text changes to detect @ symbol for mention picker.
    /// </summary>
    partial void OnInputTextChanged(string value)
    {
        DetectMentionTrigger(value);
    }

    private void DetectMentionTrigger(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            MentionPicker.Hide();
            return;
        }

        // Find the last @ symbol
        var lastAtIndex = text.LastIndexOf('@');

        if (lastAtIndex == -1)
        {
            // No @ symbol, hide picker
            if (MentionPicker.IsVisible)
                MentionPicker.Hide();
            return;
        }

        // Check if @ is at the start or preceded by whitespace (word boundary)
        var isAtWordBoundary = lastAtIndex == 0 || char.IsWhiteSpace(text[lastAtIndex - 1]);

        if (!isAtWordBoundary)
        {
            if (MentionPicker.IsVisible)
                MentionPicker.Hide();
            return;
        }

        // Extract query after @
        var query = text.Substring(lastAtIndex + 1);

        // If there's whitespace after @, it's complete, hide picker
        if (query.Contains(' ') || query.Contains('\n') || query.Contains('\r'))
        {
            if (MentionPicker.IsVisible)
                MentionPicker.Hide();
            return;
        }

        // Show picker and update query
        if (!MentionPicker.IsVisible)
        {
            MentionPicker.Show();
            _lastAtSymbolPosition = lastAtIndex;
        }

        MentionPicker.UpdateQuery(query);
    }

    private void OnMentionSelected(MentionItem item)
    {
        if (string.IsNullOrEmpty(InputText))
            return;

        // Replace the query after @ with the selected item name
        var atIndex = InputText.LastIndexOf('@');
        if (atIndex == -1)
            return;

        var beforeAt = InputText.Substring(0, atIndex);
        var mentionText = item.Category == "File"
            ? Path.GetFileName(item.Path)
            : item.Name;

        InputText = $"{beforeAt}@{mentionText} ";

        // Add to mentioned files if it's a file
        if (item.Category == "File" && !MentionedFiles.Contains(item.Path))
        {
            MentionedFiles.Add(item.Path);
        }
    }

    private void OnMentionPickerDismissed()
    {
        // Picker was dismissed, nothing to do
    }

    [RelayCommand]
    private void SendMessage()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrEmpty(text) || IsProcessing) return;

        // Add to history
        _inputHistory.Add(text);
        _historyIndex = _inputHistory.Count;

        InputText = string.Empty;

        // Clear mentioned files after sending (they'll be included in context)
        MentionedFiles.Clear();

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
