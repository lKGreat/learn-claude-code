using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents an editor group (split pane) that has its own tab bar and active tab.
/// Multiple editor groups enable split-view editing.
/// </summary>
public partial class EditorGroup : ObservableObject
{
    public int GroupId { get; init; }

    public ObservableCollection<EditorTab> Tabs { get; } = [];

    [ObservableProperty]
    private EditorTab? _activeTab;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _currentContent = "";

    public bool HasTabs => Tabs.Count > 0;

    public void ActivateTab(EditorTab tab)
    {
        if (ActiveTab != null) ActiveTab.IsActive = false;
        tab.IsActive = true;
        ActiveTab = tab;
        CurrentContent = tab.Content;
        OnPropertyChanged(nameof(HasTabs));
    }

    public void CloseTab(EditorTab tab)
    {
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            CurrentContent = "";
        }
        else if (tab == ActiveTab)
        {
            var nextIndex = Math.Min(index, Tabs.Count - 1);
            ActivateTab(Tabs[nextIndex]);
        }
        OnPropertyChanged(nameof(HasTabs));
    }
}
