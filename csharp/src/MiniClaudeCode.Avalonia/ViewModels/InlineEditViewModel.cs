using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Core.AI;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the inline edit panel (Ctrl+K style editing).
/// Manages the workflow: collect instruction → call service → show diff → accept/reject.
/// </summary>
public partial class InlineEditViewModel : ObservableObject
{
    private InlineEditService? _editService;
    private CancellationTokenSource? _cts;

    // Input state
    [ObservableProperty]
    private string _instruction = "";

    // UI state
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    // Edit context
    private string _filePath = "";
    private string _language = "";
    private string _selectedCode = "";
    private int _startLine;
    private int _endLine;
    private string _contextBefore = "";
    private string _contextAfter = "";

    // Result data
    [ObservableProperty]
    private string _originalCode = "";

    [ObservableProperty]
    private string _modifiedCode = "";

    [ObservableProperty]
    private string _diffText = "";

    // Events
    public event Action<string, string>? EditAccepted; // (filePath, modifiedCode)
    public event Action? EditDismissed;

    /// <summary>
    /// Set the edit service (called from MainWindowViewModel).
    /// </summary>
    public void SetEditService(InlineEditService? service)
    {
        _editService = service;
    }

    /// <summary>
    /// Show the inline edit panel with context.
    /// </summary>
    public void Show(
        string filePath,
        string language,
        string selectedCode,
        int startLine,
        int endLine,
        string contextBefore,
        string contextAfter)
    {
        _filePath = filePath;
        _language = language;
        _selectedCode = selectedCode;
        _startLine = startLine;
        _endLine = endLine;
        _contextBefore = contextBefore;
        _contextAfter = contextAfter;

        Instruction = "";
        OriginalCode = selectedCode;
        ModifiedCode = "";
        DiffText = "";
        HasResult = false;
        IsProcessing = false;
        ErrorMessage = null;
        IsVisible = true;
    }

    /// <summary>
    /// Submit the edit instruction to the AI service.
    /// </summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (_editService == null)
        {
            ErrorMessage = "Edit service not available";
            return;
        }

        if (string.IsNullOrWhiteSpace(Instruction))
        {
            ErrorMessage = "Please enter an instruction";
            return;
        }

        IsProcessing = true;
        ErrorMessage = null;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var request = new EditRequest
            {
                FilePath = _filePath,
                Language = _language,
                StartLine = _startLine,
                EndLine = _endLine,
                SelectedCode = _selectedCode,
                Instruction = Instruction,
                ContextBefore = _contextBefore,
                ContextAfter = _contextAfter
            };

            var result = await _editService.EditCodeAsync(request, _cts.Token);

            if (result.Success)
            {
                OriginalCode = result.OriginalCode;
                ModifiedCode = result.ModifiedCode;
                DiffText = result.Diff ?? "";
                HasResult = true;
                ErrorMessage = null;
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Edit failed";
                HasResult = false;
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Edit cancelled";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            HasResult = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Accept the edit and apply changes.
    /// </summary>
    [RelayCommand]
    private void Accept()
    {
        if (!HasResult || string.IsNullOrEmpty(ModifiedCode))
            return;

        EditAccepted?.Invoke(_filePath, ModifiedCode);
        Dismiss();
    }

    /// <summary>
    /// Reject the edit and dismiss the panel.
    /// </summary>
    [RelayCommand]
    private void Reject()
    {
        Dismiss();
    }

    /// <summary>
    /// Cancel the current operation.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        Dismiss();
    }

    /// <summary>
    /// Dismiss the panel and reset state.
    /// </summary>
    private void Dismiss()
    {
        IsVisible = false;
        _cts?.Cancel();
        _cts = null;
        EditDismissed?.Invoke();
    }
}
