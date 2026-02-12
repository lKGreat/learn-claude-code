using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
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
        }

        _viewModel = DataContext as EditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.ActiveFileChanged += OnActiveFileChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.GoToLineRequested += OnGoToLineRequested;

            // Install TextMate on the editor
            _textMateInstallation = CodeEditor.InstallTextMate(_registryOptions);
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

        // Track text changes for dirty state
        CodeEditor.TextChanged += (s, e) =>
        {
            if (_viewModel?.ActiveTab != null && !_isLoadingContent)
            {
                _viewModel.UpdateContent(CodeEditor.Text);
            }
        };
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
}
