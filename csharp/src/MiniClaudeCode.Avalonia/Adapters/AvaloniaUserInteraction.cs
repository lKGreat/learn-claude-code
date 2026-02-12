using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Adapters;

/// <summary>
/// Implements IUserInteraction by showing overlay dialogs via QuestionDialogViewModel.
/// </summary>
public class AvaloniaUserInteraction(QuestionDialogViewModel questionDialog) : IUserInteraction
{
    public async Task<string?> AskAsync(string question, CancellationToken ct = default)
    {
        string? result = null;
        await DispatcherService.InvokeAsync(() =>
        {
            // We need to start the async dialog on the UI thread
        });

        // Show the dialog on UI thread and get the task
        Task<string?> dialogTask = null!;
        await DispatcherService.InvokeAsync(() =>
        {
            dialogTask = questionDialog.AskFreeTextAsync(question);
        });

        // Wait for user response (this runs on background thread)
        result = await dialogTask;
        return result;
    }

    public async Task<string?> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken ct = default)
    {
        Task<string?> dialogTask = null!;
        await DispatcherService.InvokeAsync(() =>
        {
            dialogTask = questionDialog.AskSelectionAsync(title, choices);
        });

        return await dialogTask;
    }

    public async Task<string?> AskSecretAsync(string prompt, CancellationToken ct = default)
    {
        // Use the free-text dialog for secrets (will be handled in the view with password masking)
        Task<string?> dialogTask = null!;
        await DispatcherService.InvokeAsync(() =>
        {
            dialogTask = questionDialog.AskFreeTextAsync(prompt);
        });

        return await dialogTask;
    }

    public async Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
    {
        Task<string?> dialogTask = null!;
        await DispatcherService.InvokeAsync(() =>
        {
            dialogTask = questionDialog.AskConfirmAsync(message);
        });

        var result = await dialogTask;
        return result == "Yes";
    }
}
