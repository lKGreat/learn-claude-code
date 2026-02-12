# GhostTextAdorner Usage Guide

## Overview

`GhostTextAdorner` is a custom visual layer for AvaloniaEdit that renders AI completion suggestions as gray italic "ghost text" at the cursor position, similar to GitHub Copilot's inline suggestions.

## Features

- **Inline rendering**: Displays suggestions without modifying the document
- **Multi-line support**: Handles completions that span multiple lines
- **Visual styling**: Gray (#6C7086), italic, 60% opacity
- **Event-driven**: Fires events when accepted or dismissed
- **Robust**: Handles edge cases like scrolling, document changes, and out-of-view positions

## Basic Usage

### 1. Create and Add to TextView

```csharp
using MiniClaudeCode.Avalonia.Editor;

// In your EditorView code-behind or view model setup
var ghostTextAdorner = new GhostTextAdorner(CodeEditor.TextArea);

// Add to the TextView's background renderers
CodeEditor.TextArea.TextView.BackgroundRenderers.Add(ghostTextAdorner);
```

### 2. Display Ghost Text

```csharp
// When you receive a completion from the AI
string completionText = "const result = await fetchData();";
int caretOffset = CodeEditor.TextArea.Caret.Offset;

ghostTextAdorner.SetGhostText(completionText, caretOffset);
```

### 3. Handle User Actions

```csharp
// Subscribe to events
ghostTextAdorner.Accepted += () =>
{
    // Insert the ghost text into the document
    var text = ghostTextAdorner.GhostText;
    if (!string.IsNullOrEmpty(text))
    {
        CodeEditor.Document.Insert(CodeEditor.TextArea.Caret.Offset, text);
    }
};

ghostTextAdorner.Dismissed += () =>
{
    // Clean up or log dismissal
    Console.WriteLine("Ghost text dismissed");
};

// Accept on Tab key
CodeEditor.TextArea.KeyDown += (s, e) =>
{
    if (e.Key == Key.Tab && !string.IsNullOrEmpty(ghostTextAdorner.GhostText))
    {
        e.Handled = true;
        ghostTextAdorner.Accept(); // Fires Accepted event
    }
};

// Dismiss on Escape
CodeEditor.TextArea.KeyDown += (s, e) =>
{
    if (e.Key == Key.Escape && !string.IsNullOrEmpty(ghostTextAdorner.GhostText))
    {
        e.Handled = true;
        ghostTextAdorner.Clear(); // Fires Dismissed event
    }
};
```

## Full Integration Example

Here's a complete example showing how to integrate ghost text with an AI completion service:

```csharp
public partial class EditorView : UserControl
{
    private GhostTextAdorner? _ghostTextAdorner;
    private CancellationTokenSource? _completionCts;

    public EditorView()
    {
        InitializeComponent();

        // Initialize ghost text adorner
        _ghostTextAdorner = new GhostTextAdorner(
            CodeEditor.TextArea,
            FontFamily.Parse("Cascadia Code, Consolas, monospace"),
            fontSize: 13);

        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_ghostTextAdorner);

        // Set up event handlers
        _ghostTextAdorner.Accepted += OnGhostTextAccepted;
        _ghostTextAdorner.Dismissed += OnGhostTextDismissed;

        // Handle keyboard input
        CodeEditor.TextArea.KeyDown += OnEditorKeyDown;
        CodeEditor.TextArea.TextEntered += OnTextEntered;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // Accept ghost text on Tab
        if (e.Key == Key.Tab && !string.IsNullOrEmpty(_ghostTextAdorner?.GhostText))
        {
            e.Handled = true;
            _ghostTextAdorner.Accept();
            return;
        }

        // Dismiss on Escape
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(_ghostTextAdorner?.GhostText))
        {
            e.Handled = true;
            _ghostTextAdorner.Clear();
            return;
        }

        // Cancel pending completion on navigation keys
        if (e.Key == Key.Left || e.Key == Key.Right ||
            e.Key == Key.Up || e.Key == Key.Down)
        {
            CancelPendingCompletion();
        }
    }

    private async void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        // Dismiss current ghost text when user types
        _ghostTextAdorner?.Clear();

        // Request new completion after a short delay
        CancelPendingCompletion();
        _completionCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _completionCts.Token); // Debounce
            await RequestCompletionAsync(_completionCts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected when typing continues
        }
    }

    private async Task RequestCompletionAsync(CancellationToken cancellationToken)
    {
        var text = CodeEditor.Text;
        var offset = CodeEditor.TextArea.Caret.Offset;

        // Call your AI service
        var completion = await YourAIService.GetCompletionAsync(text, offset, cancellationToken);

        if (!string.IsNullOrEmpty(completion) && !cancellationToken.IsCancellationRequested)
        {
            _ghostTextAdorner?.SetGhostText(completion, offset);
        }
    }

    private void OnGhostTextAccepted()
    {
        var text = _ghostTextAdorner?.GhostText;
        if (!string.IsNullOrEmpty(text))
        {
            var offset = CodeEditor.TextArea.Caret.Offset;
            CodeEditor.Document.Insert(offset, text);
        }
    }

    private void OnGhostTextDismissed()
    {
        CancelPendingCompletion();
    }

    private void CancelPendingCompletion()
    {
        _completionCts?.Cancel();
        _completionCts = null;
    }
}
```

## API Reference

### Constructor

```csharp
public GhostTextAdorner(
    TextArea textArea,
    FontFamily? fontFamily = null,
    double fontSize = 13)
```

**Parameters:**
- `textArea`: The AvaloniaEdit TextArea to render in
- `fontFamily`: Optional font family (defaults to "Cascadia Code, Consolas, Menlo, monospace")
- `fontSize`: Optional font size (defaults to 13)

### Properties

```csharp
public string? GhostText { get; set; }
```
Gets or sets the ghost text to display. Set to `null` to hide.

```csharp
public KnownLayer Layer { get; }
```
Returns `KnownLayer.Caret` to render on top of text.

### Methods

```csharp
public void SetGhostText(string? text, int offset)
```
Sets both the ghost text and its position in one call.

```csharp
public void SetPosition(int offset)
```
Updates the position where ghost text appears.

```csharp
public void Clear()
```
Clears and hides the ghost text. Fires the `Dismissed` event.

```csharp
public void Accept()
```
Accepts the ghost text. Fires the `Accepted` event, then clears.

### Events

```csharp
public event Action? Accepted
```
Fired when the user accepts the ghost text (e.g., presses Tab).

```csharp
public event Action? Dismissed
```
Fired when the ghost text is dismissed (e.g., cleared or user navigates away).

## Multi-Line Completions

The adorner automatically handles multi-line ghost text:

```csharp
var multiLineCompletion = @"function calculate() {
    const x = 10;
    const y = 20;
    return x + y;
}";

ghostTextAdorner.SetGhostText(multiLineCompletion, caretOffset);
```

Each line will be rendered with proper line spacing, maintaining the editor's font metrics.

## Best Practices

1. **Debounce requests**: Don't request completions on every keystroke. Use a 200-300ms delay.
2. **Cancel previous requests**: Cancel pending AI requests when the user types.
3. **Clear on navigation**: Dismiss ghost text when the cursor moves.
4. **Handle edge cases**: The adorner handles scrolling and document changes, but you should still validate offsets.
5. **Resource cleanup**: Remove the renderer from `BackgroundRenderers` when disposing the view.

## Visual Styling

The ghost text uses these default styles (defined as constants):
- **Color**: `#6C7086` (Catppuccin Mocha gray)
- **Opacity**: `0.6` (60%)
- **Font Style**: `Italic`
- **Font Weight**: `Normal`

These can be modified by editing the `GhostTextAdorner.cs` source file.

## Troubleshooting

### Ghost text not visible
- Verify the offset is within the visible viewport
- Check that the TextView has been rendered (try after `Loaded` event)
- Ensure `InvalidateLayer()` is being called

### Ghost text at wrong position
- Verify the offset calculation is correct
- Check for document modifications between getting offset and showing ghost text

### Performance issues
- Limit frequency of completions with debouncing
- Keep ghost text reasonably short (< 1000 characters)
- Cancel unnecessary redraws

## Related Classes

- `AvaloniaEdit.Rendering.IBackgroundRenderer`: Interface implemented by this adorner
- `AvaloniaEdit.Rendering.TextView`: The view where rendering occurs
- `AvaloniaEdit.Editing.TextArea`: Contains the caret and text view
