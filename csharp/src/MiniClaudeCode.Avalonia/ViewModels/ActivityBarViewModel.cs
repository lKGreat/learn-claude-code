using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the VS Code-style Activity Bar (left icon strip).
/// Each item toggles the corresponding sidebar panel.
/// </summary>
public partial class ActivityBarViewModel : ObservableObject
{
    public ObservableCollection<ActivityBarItem> Items { get; } = [];

    [ObservableProperty]
    private ActivityBarItem? _activeItem;

    /// <summary>
    /// Fired when the active sidebar panel should change.
    /// Passes the panel Id, or null to collapse sidebar.
    /// </summary>
    public event Action<string?>? ActivePanelChanged;

    public ActivityBarViewModel()
    {
        Items.Add(new ActivityBarItem { Id = "explorer", Label = "Explorer", Icon = "\U0001F4C1", Shortcut = "Ctrl+Shift+E", Order = 0 });
        Items.Add(new ActivityBarItem { Id = "search", Label = "Search", Icon = "\U0001F50D", Shortcut = "Ctrl+Shift+F", Order = 1 });
        Items.Add(new ActivityBarItem { Id = "scm", Label = "Source Control", Icon = "\U0001F500", Shortcut = "Ctrl+Shift+G", Order = 2 });
        Items.Add(new ActivityBarItem { Id = "extensions", Label = "Extensions", Icon = "\U0001F9E9", Shortcut = "Ctrl+Shift+X", Order = 3 });
        Items.Add(new ActivityBarItem { Id = "chat", Label = "AI Chat", Icon = "\U0001F4AC", Shortcut = "Ctrl+Shift+C", Order = 4 });

        // Default: explorer active
        ActiveItem = Items[0];
        ActiveItem.IsActive = true;
    }

    [RelayCommand]
    private void SelectItem(ActivityBarItem? item)
    {
        if (item == null) return;

        if (item == ActiveItem)
        {
            // Toggle: clicking active item collapses sidebar
            item.IsActive = false;
            ActiveItem = null;
            ActivePanelChanged?.Invoke(null);
        }
        else
        {
            // Switch to new item
            if (ActiveItem != null) ActiveItem.IsActive = false;
            item.IsActive = true;
            ActiveItem = item;
            ActivePanelChanged?.Invoke(item.Id);
        }
    }

    /// <summary>
    /// Programmatically activate a panel by ID.
    /// </summary>
    public void ActivatePanel(string panelId)
    {
        var item = Items.FirstOrDefault(i => i.Id == panelId);
        if (item != null) SelectItem(item);
    }

    /// <summary>
    /// Update the badge count on a specific activity bar item.
    /// </summary>
    public void SetBadge(string panelId, int count)
    {
        var item = Items.FirstOrDefault(i => i.Id == panelId);
        if (item != null) item.BadgeCount = count;
    }
}
