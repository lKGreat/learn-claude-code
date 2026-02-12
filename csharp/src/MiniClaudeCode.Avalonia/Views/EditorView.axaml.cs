using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using MiniClaudeCode.Avalonia.Editor.Folding;
using MiniClaudeCode.Avalonia.Editor.Rendering;
using MiniClaudeCode.Avalonia.Logging;
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
    private SearchResultRenderer? _searchRenderer;
    private IndentGuideRenderer? _indentGuideRenderer;
    private FoldingManager? _foldingManager;
    private IndentFoldingStrategy? _foldingStrategy;
    private DispatcherTimer? _foldingTimer;
    private List<(int Offset, int Length)> _searchMatches = [];

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
            _viewModel.FindReplace.SearchRequested -= OnSearchRequested;
            _viewModel.FindReplace.FindNextRequested -= OnFindNext;
            _viewModel.FindReplace.FindPreviousRequested -= OnFindPrevious;
            _viewModel.FindReplace.ReplaceCurrentRequested -= OnReplaceCurrent;
            _viewModel.FindReplace.ReplaceAllRequested -= OnReplaceAll;
            _viewModel.FindReplace.CloseRequested -= OnFindReplaceClose;
        }

        _viewModel = DataContext as EditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.ActiveFileChanged += OnActiveFileChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.GoToLineRequested += OnGoToLineRequested;
            _viewModel.InlineEditAccepted += OnInlineEditAccepted;

            // Temporarily disable TextMate theming to ensure text remains visible.
            // We can re-enable after resolving style/theme compatibility issues.
            _textMateInstallation = null;

            // Install ghost text renderer
            _ghostTextRenderer = new GhostTextRenderer(_viewModel);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_ghostTextRenderer);

            // Install search result renderer
            _searchRenderer = new SearchResultRenderer();
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_searchRenderer);

            // Install indent guide renderer
            _indentGuideRenderer = new IndentGuideRenderer();
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentGuideRenderer);

            // Set up code folding
            _foldingStrategy = new IndentFoldingStrategy();
            _foldingManager = FoldingManager.Install(CodeEditor.TextArea);

            // Timer to update foldings periodically (avoid updating on every keystroke)
            _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _foldingTimer.Tick += (_, _) => UpdateFoldings();
            _foldingTimer.Start();

            // Attach minimap to editor
            Minimap.AttachEditor(CodeEditor);

            // Wire up breadcrumb navigation
            _viewModel.BreadcrumbNav.NavigateRequested += path =>
            {
                if (File.Exists(path))
                    _viewModel.OpenFile(path);
            };

            // Wire up find/replace events
            _viewModel.FindReplace.SearchRequested += OnSearchRequested;
            _viewModel.FindReplace.FindNextRequested += OnFindNext;
            _viewModel.FindReplace.FindPreviousRequested += OnFindPrevious;
            _viewModel.FindReplace.ReplaceCurrentRequested += OnReplaceCurrent;
            _viewModel.FindReplace.ReplaceAllRequested += OnReplaceAll;
            _viewModel.FindReplace.CloseRequested += OnFindReplaceClose;

            // Suppress AvaloniaEdit built-in search panel
            try
            {
                var searchPanel = AvaloniaEdit.Search.SearchPanel.Install(CodeEditor);
                searchPanel.Close();
            }
            catch { /* ignore if not available */ }
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
        if (e.PropertyName == nameof(EditorViewModel.IsIndentGuidesVisible))
        {
            if (_indentGuideRenderer != null)
            {
                if (_viewModel!.IsIndentGuidesVisible)
                {
                    if (!CodeEditor.TextArea.TextView.BackgroundRenderers.Contains(_indentGuideRenderer))
                        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentGuideRenderer);
                }
                else
                {
                    CodeEditor.TextArea.TextView.BackgroundRenderers.Remove(_indentGuideRenderer);
                }
                CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            }
        }
        else if (e.PropertyName == nameof(EditorViewModel.IsFoldingEnabled))
        {
            if (_viewModel is { IsFoldingEnabled: true })
            {
                if (_foldingManager == null)
                    _foldingManager = FoldingManager.Install(CodeEditor.TextArea);
                UpdateFoldings();
            }
            else if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }
        }
    }

    private void OnActiveFileChanged(EditorTab? tab)
    {
        if (tab == null)
        {
            // All tabs closed: clear editor content
            LogHelper.UI.Debug("OnActiveFileChanged: tab=null, 清空编辑器");
            _isLoadingContent = true;
            try
            {
                CodeEditor.Document = new TextDocument("");
            }
            finally
            {
                _isLoadingContent = false;
            }
            return;
        }

        // Preferred: swap AvaloniaEdit Document per tab (more reliable than re-assigning Text)
        tab.Document ??= new TextDocument(tab.Content ?? string.Empty);
        LogHelper.UI.Info("[Preview链路] EditorView.OnActiveFileChanged: Path={0}, IsPreview={1}, Language={2}, DocLength={3}",
            tab.FilePath, tab.IsPreview, tab.Language, tab.Document.TextLength);

        _isLoadingContent = true;
        try
        {
            // Document 切换时，清空“旧文档偏移量”的状态（搜索高亮/选区），否则 renderer 可能越界抛 ArgumentException
            try
            {
                _searchMatches.Clear();
                _searchRenderer?.Clear();
                CodeEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
            }
            catch { /* best-effort */ }

            // AvaloniaEdit 在切换 Document 时可能因旧 caret/selection offset 越界抛 ArgumentException（first-chance）。
            // 先将 caret/selection 归零，再赋值 Document，可显著降低该异常。
            try
            {
                CodeEditor.TextArea.ClearSelection();
                CodeEditor.TextArea.Caret.Offset = 0;
            }
            catch
            {
                // ignore: best-effort
            }

            try
            {
                CodeEditor.Document = tab.Document;
            }
            catch (ArgumentException ex)
            {
                LogHelper.UI.Error(ex,
                    "[Preview链路] EditorView.OnActiveFileChanged: CodeEditor.Document 赋值失败(ArgumentException). Path={0}, DocLength={1}",
                    tab.FilePath, tab.Document.TextLength);

                // 自愈：重建一个新的 TextDocument（避免复用 Document 时内部锚点/segment 状态不一致）
                var fresh = new TextDocument(tab.Document.Text);
                tab.Document = fresh;
                CodeEditor.Document = fresh;
            }

            try
            {
                CodeEditor.TextArea.Caret.Offset = 0;
            }
            catch
            {
                // ignore: best-effort
            }
        }
        finally
        {
            _isLoadingContent = false;
        }

        ApplyLanguageGrammar(tab.Language);
        UpdateFoldings();
    }

    private void UpdateFoldings()
    {
        if (_viewModel is not { IsFoldingEnabled: true }) return;
        if (_foldingManager == null || _foldingStrategy == null || CodeEditor.Document == null) return;

        try
        {
            _foldingStrategy.UpdateFoldings(_foldingManager, CodeEditor.Document);
        }
        catch (Exception ex)
        {
            LogHelper.UI.Warn(ex, "折叠更新错误");
        }
    }

    // Note: content loading is now driven by swapping CodeEditor.Document per-tab.

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

        // Ctrl+F: Show find
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            _viewModel.ShowFind();
            return;
        }

        // Ctrl+H: Show find and replace
        if (e.Key == Key.H && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            _viewModel.ShowReplace();
            return;
        }

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

    // =========================================================================
    // Find & Replace Support
    // =========================================================================

    private void OnSearchRequested(string searchText, bool caseSensitive, bool wholeWord, bool isRegex)
    {
        if (_viewModel == null || _searchRenderer == null || CodeEditor.Document == null) return;

        if (string.IsNullOrEmpty(searchText))
        {
            _searchMatches.Clear();
            _searchRenderer.Clear();
            _viewModel.FindReplace.MatchCount = 0;
            _viewModel.FindReplace.CurrentMatchIndex = -1;
            CodeEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
            return;
        }

        _searchMatches = SearchResultRenderer.FindAll(
            CodeEditor.Document, searchText, caseSensitive, wholeWord, isRegex);

        _viewModel.FindReplace.MatchCount = _searchMatches.Count;

        if (_searchMatches.Count > 0)
        {
            // Find the match closest to the caret
            var caretOffset = CodeEditor.TextArea.Caret.Offset;
            var idx = _searchMatches.FindIndex(m => m.Offset >= caretOffset);
            if (idx < 0) idx = 0;
            _viewModel.FindReplace.CurrentMatchIndex = idx;
            _searchRenderer.SetMatches(_searchMatches, idx);
        }
        else
        {
            _viewModel.FindReplace.CurrentMatchIndex = -1;
            _searchRenderer.SetMatches(_searchMatches);
        }

        CodeEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
    }

    private void OnFindNext()
    {
        if (_viewModel == null || _searchMatches.Count == 0) return;

        var idx = _viewModel.FindReplace.CurrentMatchIndex + 1;
        if (idx >= _searchMatches.Count) idx = 0;

        _viewModel.FindReplace.CurrentMatchIndex = idx;
        _searchRenderer?.SetMatches(_searchMatches, idx);
        NavigateToMatch(idx);
    }

    private void OnFindPrevious()
    {
        if (_viewModel == null || _searchMatches.Count == 0) return;

        var idx = _viewModel.FindReplace.CurrentMatchIndex - 1;
        if (idx < 0) idx = _searchMatches.Count - 1;

        _viewModel.FindReplace.CurrentMatchIndex = idx;
        _searchRenderer?.SetMatches(_searchMatches, idx);
        NavigateToMatch(idx);
    }

    private void NavigateToMatch(int idx)
    {
        if (idx < 0 || idx >= _searchMatches.Count) return;

        var (offset, length) = _searchMatches[idx];
        CodeEditor.TextArea.Caret.Offset = offset;
        CodeEditor.TextArea.Caret.BringCaretToView();
        CodeEditor.Select(offset, length);
        CodeEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
    }

    private void OnReplaceCurrent(string replaceText)
    {
        if (_viewModel == null || _searchMatches.Count == 0) return;

        var idx = _viewModel.FindReplace.CurrentMatchIndex;
        if (idx < 0 || idx >= _searchMatches.Count) return;

        var (offset, length) = _searchMatches[idx];
        CodeEditor.Document.Replace(offset, length, replaceText);

        // Re-search after replace
        OnSearchRequested(_viewModel.FindReplace.SearchText,
            _viewModel.FindReplace.IsCaseSensitive,
            _viewModel.FindReplace.IsWholeWord,
            _viewModel.FindReplace.IsRegex);
    }

    private void OnReplaceAll()
    {
        if (_viewModel == null || _searchMatches.Count == 0) return;

        // Replace from bottom to top to preserve offsets
        for (int i = _searchMatches.Count - 1; i >= 0; i--)
        {
            var (offset, length) = _searchMatches[i];
            CodeEditor.Document.Replace(offset, length, _viewModel.FindReplace.ReplaceText);
        }

        // Re-search after replace all
        OnSearchRequested(_viewModel.FindReplace.SearchText,
            _viewModel.FindReplace.IsCaseSensitive,
            _viewModel.FindReplace.IsWholeWord,
            _viewModel.FindReplace.IsRegex);
    }

    private void OnFindReplaceClose()
    {
        _searchMatches.Clear();
        _searchRenderer?.Clear();
        CodeEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
        CodeEditor.Focus();
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
