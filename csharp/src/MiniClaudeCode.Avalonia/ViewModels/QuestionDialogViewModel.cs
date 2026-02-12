using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the agent question overlay dialog.
/// </summary>
public partial class QuestionDialogViewModel : ObservableObject
{
    private TaskCompletionSource<string?>? _tcs;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _title = "Question";

    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private bool _isMultiSelect;

    [ObservableProperty]
    private string _freeTextInput = string.Empty;

    [ObservableProperty]
    private bool _isFreeTextMode;

    public ObservableCollection<QuestionOption> Options { get; } = [];

    /// <summary>
    /// Show a selection question dialog.
    /// </summary>
    public Task<string?> AskSelectionAsync(string title, IReadOnlyList<string> choices)
    {
        _tcs = new TaskCompletionSource<string?>();
        Title = title;
        Prompt = title;
        IsFreeTextMode = false;
        IsMultiSelect = false;

        Options.Clear();
        foreach (var choice in choices)
            Options.Add(new QuestionOption { Label = choice });

        if (Options.Count > 0)
            Options[0].IsSelected = true;

        IsVisible = true;
        return _tcs.Task;
    }

    /// <summary>
    /// Show a free-text question dialog.
    /// </summary>
    public Task<string?> AskFreeTextAsync(string question)
    {
        _tcs = new TaskCompletionSource<string?>();
        Title = "Question";
        Prompt = question;
        IsFreeTextMode = true;
        FreeTextInput = string.Empty;
        Options.Clear();

        IsVisible = true;
        return _tcs.Task;
    }

    /// <summary>
    /// Show a confirmation dialog.
    /// </summary>
    public Task<string?> AskConfirmAsync(string message)
    {
        _tcs = new TaskCompletionSource<string?>();
        Title = "Confirm";
        Prompt = message;
        IsFreeTextMode = false;
        IsMultiSelect = false;

        Options.Clear();
        Options.Add(new QuestionOption { Label = "Yes", IsSelected = true });
        Options.Add(new QuestionOption { Label = "No" });

        IsVisible = true;
        return _tcs.Task;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (IsFreeTextMode)
        {
            _tcs?.TrySetResult(FreeTextInput);
        }
        else
        {
            var selected = Options.Where(o => o.IsSelected).Select(o => o.Label).ToList();
            _tcs?.TrySetResult(selected.Count > 0 ? string.Join(", ", selected) : null);
        }
        IsVisible = false;
    }

    [RelayCommand]
    private void CancelDialog()
    {
        _tcs?.TrySetResult(null);
        IsVisible = false;
    }

    public void SelectOption(QuestionOption option)
    {
        if (!IsMultiSelect)
        {
            foreach (var o in Options) o.IsSelected = false;
        }
        option.IsSelected = !option.IsSelected;
    }
}

public partial class QuestionOption : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
