using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.ViewModels;
using TextMateSharp.Grammars;

namespace MiniClaudeCode.Avalonia.Views;

public partial class EditorView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private readonly RegistryOptions _registryOptions;
    private EditorViewModel? _viewModel;
    private GhostTextRenderer? _ghostTextRenderer;

    public EditorView()
    {
        InitializeComponent();

        // Initialize TextMate with Dark Plus theme
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ActiveFileChanged -= OnActiveFileChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.GoToLineRequested -= OnGoToLineRequested;
            _viewModel.InlineEditAccepted -= OnInlineEditAccepted;
        }

        _viewModel = DataContext as EditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.ActiveFileChanged += OnActiveFileChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.GoToLineRequested += OnGoToLineRequested;
            _viewModel.InlineEditAccepted += OnInlineEditAccepted;

            // Install TextMate on the editor
            _textMateInstallation = CodeEditor.InstallTextMate(_registryOptions);

            // Install ghost text renderer
            _ghostTextRenderer = new GhostTextRenderer(_viewModel);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_ghostTextRenderer);
        }

        // Set up caret position tracking
        CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            if (_viewModel != null)
            {
                _viewModel.UpdateCursorPosition(
                    CodeEditor.TextArea.Caret.Line,
                    CodeEditor.TextArea.Caret.Column);
            }
        };

        // Track text changes for dirty state and inline completion
        CodeEditor.TextChanged += (s, e) =>
        {
            if (_viewModel?.ActiveTab != null && !_isLoadingContent)
            {
                _viewModel.UpdateContent(CodeEditor.Text);

                // Request inline completion after text change (with debounce)
                _ = RequestCompletionAsync();
            }
        };

        // Handle key events for completion accept/dismiss and inline edit
        CodeEditor.TextArea.KeyDown += OnEditorKeyDown;
    }

    private bool _isLoadingContent;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.CurrentContent))
        {
            LoadContent();
        }
    }

    private void OnActiveFileChanged(EditorTab? tab)
    {
        if (tab == null) return;

        LoadContent();
        ApplyLanguageGrammar(tab.Language);
    }

    private void LoadContent()
    {
        if (_viewModel == null) return;

        _isLoadingContent = true;
        try
        {
            CodeEditor.Text = _viewModel.CurrentContent;
        }
        finally
        {
            _isLoadingContent = false;
        }
    }

    private void ApplyLanguageGrammar(string language)
    {
        if (_textMateInstallation == null) return;

        try
        {
            // Map language id to TextMate scope name
            var scopeName = language switch
            {
                "csharp" => "source.cs",
                "typescript" => "source.ts",
                "javascript" => "source.js",
                "python" => "source.python",
                "json" => "source.json",
                "xml" => "text.xml",
                "html" => "text.html.basic",
                "css" => "source.css",
                "markdown" => "text.html.markdown",
                "yaml" => "source.yaml",
                "rust" => "source.rust",
                "go" => "source.go",
                "java" => "source.java",
                "cpp" => "source.cpp",
                "c" => "source.c",
                "shell" => "source.shell",
                "powershell" => "source.powershell",
                "sql" => "source.sql",
                _ => null
            };

            if (scopeName != null)
            {
                _textMateInstallation.SetGrammar(scopeName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set grammar for {language}: {ex.Message}");
        }
    }

    private void OnGoToLineRequested(int line)
    {
        if (line < 1) return;

        // Clamp line to document range
        var maxLine = CodeEditor.Document?.LineCount ?? 1;
        line = Math.Min(line, maxLine);

        // Set caret position and scroll into view
        var docLine = CodeEditor.Document?.GetLineByNumber(line);
        if (docLine != null)
        {
            CodeEditor.TextArea.Caret.Line = line;
            CodeEditor.TextArea.Caret.Column = 1;
            CodeEditor.TextArea.Caret.BringCaretToView();
            CodeEditor.ScrollToLine(line);
        }
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is EditorTab tab && _viewModel != null)
        {
            _viewModel.ActivateTab(tab);
        }
    }

    // =========================================================================
    // Inline Completion Support
    // =========================================================================

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // Ctrl+K: Show inline edit
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            ShowInlineEditPanel();
            return;
        }

        // Tab key: accept completion if ghost text is visible
        if (e.Key == Key.Tab && !string.IsNullOrEmpty(_viewModel.GhostText))
        {
            e.Handled = true;
            var completionText = _viewModel.AcceptCompletion();
            if (completionText != null)
            {
                // Insert the completion text at current caret position
                var caretOffset = CodeEditor.TextArea.Caret.Offset;
                CodeEditor.Document.Insert(caretOffset, completionText);
            }
            return;
        }

        // Escape key: dismiss completion if ghost text is visible
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(_viewModel.GhostText))
        {
            e.Handled = true;
            _viewModel.DismissCompletion();
            return;
        }

        // Any other typing: cancel pending completion and dismiss ghost text
        if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.Left && e.Key != Key.Right &&
            e.Key != Key.Home && e.Key != Key.End && e.Key != Key.PageUp && e.Key != Key.PageDown)
        {
            _viewModel.CancelPendingCompletion();
        }
    }

    /// <summary>
    /// Show the inline edit panel for the current selection.
    /// </summary>
    private void ShowInlineEditPanel()
    {
        if (_viewModel == null || !_viewModel.HasOpenFiles)
            return;

        var selection = CodeEditor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            // No selection - select current line
            var currentLine = CodeEditor.Document.GetLineByNumber(CodeEditor.TextArea.Caret.Line);
            CodeEditor.TextArea.Selection = AvaloniaEdit.Editing.Selection.Create(
                CodeEditor.TextArea,
                currentLine.Offset,
                currentLine.EndOffset);
        }

        // Get selection info
        var selectedText = CodeEditor.SelectedText;
        if (string.IsNullOrWhiteSpace(selectedText))
            return;

        var startOffset = CodeEditor.SelectionStart;
        var endOffset = CodeEditor.SelectionStart + CodeEditor.SelectionLength;

        var startLine = CodeEditor.Document.GetLineByOffset(startOffset);
        var endLine = CodeEditor.Document.GetLineByOffset(endOffset);

        // Get context before and after selection
        var contextBefore = GetContextBefore(startLine.LineNumber);
        var contextAfter = GetContextAfter(endLine.LineNumber);

        _viewModel.ShowInlineEdit(
            selectedText,
            startLine.LineNumber,
            endLine.LineNumber,
            contextBefore,
            contextAfter);
    }

    /// <summary>
    /// Get context lines before the selection (up to 20 lines).
    /// </summary>
    private string GetContextBefore(int startLine)
    {
        var contextLines = new List<string>();
        var lineCount = Math.Min(20, startLine - 1);

        for (int i = startLine - lineCount; i < startLine; i++)
        {
            if (i >= 1 && i <= CodeEditor.Document.LineCount)
            {
                var line = CodeEditor.Document.GetLineByNumber(i);
                contextLines.Add(CodeEditor.Document.GetText(line.Offset, line.Length));
            }
        }

        return string.Join('\n', contextLines);
    }

    /// <summary>
    /// Get context lines after the selection (up to 20 lines).
    /// </summary>
    private string GetContextAfter(int endLine)
    {
        var contextLines = new List<string>();
        var lineCount = Math.Min(20, CodeEditor.Document.LineCount - endLine);

        for (int i = endLine + 1; i <= endLine + lineCount && i <= CodeEditor.Document.LineCount; i++)
        {
            var line = CodeEditor.Document.GetLineByNumber(i);
            contextLines.Add(CodeEditor.Document.GetText(line.Offset, line.Length));
        }

        return string.Join('\n', contextLines);
    }

    /// <summary>
    /// Apply the inline edit result to the editor.
    /// </summary>
    private void OnInlineEditAccepted(string filePath, string modifiedCode)
    {
        if (_viewModel?.ActiveTab == null || _viewModel.ActiveTab.FilePath != filePath)
            return;

        // Replace the selected text with the modified code
        var selection = CodeEditor.TextArea.Selection;
        if (!selection.IsEmpty)
        {
            var startOffset = CodeEditor.SelectionStart;
            var length = CodeEditor.SelectionLength;

            // Replace text
            CodeEditor.Document.Replace(startOffset, length, modifiedCode);

            // Update caret position to end of replacement
            CodeEditor.TextArea.Caret.Offset = startOffset + modifiedCode.Length;

            // Clear selection
            CodeEditor.TextArea.ClearSelection();
        }
    }

    private async Task RequestCompletionAsync()
    {
        if (_viewModel == null) return;

        var text = CodeEditor.Text;
        var line = CodeEditor.TextArea.Caret.Line;
        var column = CodeEditor.TextArea.Caret.Column;

        await _viewModel.RequestCompletionAsync(text, line, column);

        // Redraw the text area to show ghost text
        CodeEditor.TextArea.TextView.InvalidateVisual();
    }
}

/// <summary>
/// Custom background renderer that displays ghost text (inline completion suggestions).
/// </summary>
internal class GhostTextRenderer : IBackgroundRenderer
{
    private readonly EditorViewModel _viewModel;

    public GhostTextRenderer(EditorViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(_viewModel.GhostText))
            return;

        // Get text area to access caret and font properties
        var textArea = textView.GetService(typeof(AvaloniaEdit.Editing.TextArea)) as AvaloniaEdit.Editing.TextArea;
        if (textArea == null)
            return;

        try
        {
            // Get caret offset
            var caretOffset = textArea.Caret.Offset;
            var location = textView.Document.GetLocation(caretOffset);

            // Find the visual line containing the caret
            var visualLine = textView.VisualLines.FirstOrDefault(vl =>
                vl.FirstDocumentLine.LineNumber <= location.Line &&
                location.Line <= vl.LastDocumentLine.LineNumber);

            if (visualLine == null)
                return;

            // Get visual position
            var visualColumn = visualLine.GetVisualColumn(caretOffset - visualLine.FirstDocumentLine.Offset);
            var xPos = visualLine.GetVisualPosition(visualColumn, VisualYPosition.LineTop).X;
            var textLine = visualLine.GetTextLine(visualColumn);
            var yPos = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineTop);

            // Create formatted text with italic gray style
            var formattedText = new FormattedText(
                _viewModel.GhostText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Italic, FontWeight.Normal),
                13, // Default editor font size
                new SolidColorBrush(Color.Parse("#6C7086"), 0.6));

            drawingContext.DrawText(formattedText, new Point(xPos, yPos));
        }
        catch
        {
            // Silently fail if rendering fails
        }
    }
}
