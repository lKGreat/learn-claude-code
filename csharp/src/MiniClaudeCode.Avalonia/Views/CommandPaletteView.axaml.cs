using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class CommandPaletteView : UserControl
{
    public CommandPaletteView()
    {
        InitializeComponent();
        
        // Auto-focus input when visible
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(IsVisible) && DataContext is CommandPaletteViewModel vm && vm.IsVisible)
            {
                QueryInput?.Focus();
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not CommandPaletteViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                vm.HideCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                vm.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                vm.MoveUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                vm.MoveDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Tab:
                // Tab cycles through modes like VS Code
                vm.MoveDownCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
