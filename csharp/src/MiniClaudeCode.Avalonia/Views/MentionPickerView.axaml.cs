using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

/// <summary>
/// View for the mention picker that handles keyboard navigation.
/// </summary>
public partial class MentionPickerView : UserControl
{
    public MentionPickerView()
    {
        InitializeComponent();

        // Handle keyboard input
        KeyDown += OnKeyDown;

        // Handle pointer interaction with items
        AddHandler(PointerPressedEvent, OnItemPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MentionPickerViewModel vm) return;

        switch (e.Key)
        {
            case Key.Down:
                vm.SelectNext();
                e.Handled = true;
                break;

            case Key.Up:
                vm.SelectPrevious();
                e.Handled = true;
                break;

            case Key.Enter:
                vm.ConfirmSelection();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.Dismiss();
                e.Handled = true;
                break;
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MentionPickerViewModel vm) return;

        // Find which item was clicked
        var source = e.Source as Control;
        while (source != null)
        {
            if (source.DataContext is MentionItem item)
            {
                var index = vm.Items.IndexOf(item);
                if (index >= 0)
                {
                    vm.SelectedIndex = index;
                    vm.ConfirmSelection();
                    e.Handled = true;
                }
                break;
            }
            source = source.Parent as Control;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Auto-focus when shown
        if (change.Property.Name == nameof(IsVisible) && IsVisible)
        {
            Focus();
        }
    }
}
