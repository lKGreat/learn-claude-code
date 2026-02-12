using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class InlineEditView : UserControl
{
    private InlineEditViewModel? _viewModel;

    public InlineEditView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as InlineEditViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InlineEditViewModel.IsVisible) && _viewModel?.IsVisible == true)
        {
            // Focus the instruction textbox when panel becomes visible
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                InstructionTextBox.Focus();
            }, global::Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Handle keyboard shortcuts
        InstructionTextBox.KeyDown += OnInstructionKeyDown;
        this.KeyDown += OnPanelKeyDown;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        InstructionTextBox.KeyDown -= OnInstructionKeyDown;
        this.KeyDown -= OnPanelKeyDown;
    }

    private void OnInstructionKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // Enter (without Shift): Submit
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            if (_viewModel.SubmitCommand.CanExecute(null))
            {
                _ = _viewModel.SubmitCommand.ExecuteAsync(null);
            }
        }

        // Escape: Cancel
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _viewModel.CancelCommand.Execute(null);
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // Ctrl+Enter: Accept result
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            if (_viewModel.AcceptCommand.CanExecute(null))
            {
                _viewModel.AcceptCommand.Execute(null);
            }
        }

        // Escape: Reject/Cancel
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_viewModel.HasResult)
            {
                _viewModel.RejectCommand.Execute(null);
            }
            else
            {
                _viewModel.CancelCommand.Execute(null);
            }
        }
    }
}
