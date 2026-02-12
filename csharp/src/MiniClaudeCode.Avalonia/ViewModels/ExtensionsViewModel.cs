using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Extensions;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Extensions panel in the sidebar.
/// </summary>
public partial class ExtensionsViewModel : ObservableObject
{
    private readonly ExtensionHost _extensionHost;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<ExtensionListItem> InstalledExtensions { get; } = [];
    public ObservableCollection<ExtensionListItem> FilteredExtensions { get; } = [];

    public string InstalledHeader => $"Installed ({InstalledExtensions.Count})";

    public ExtensionsViewModel()
    {
        _extensionHost = new ExtensionHost();
        _extensionHost.ExtensionLoaded += OnExtensionLoaded;
        _extensionHost.ExtensionActivated += OnExtensionActivated;
    }

    public ExtensionHost ExtensionHost => _extensionHost;

    partial void OnSearchQueryChanged(string value)
    {
        FilterExtensions(value);
    }

    [RelayCommand]
    private async Task DiscoverExtensions()
    {
        IsLoading = true;
        try
        {
            await _extensionHost.DiscoverExtensionsAsync();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(InstalledHeader));
        }
    }

    [RelayCommand]
    private async Task ActivateExtension(ExtensionListItem? item)
    {
        if (item == null) return;
        await _extensionHost.ActivateExtensionAsync(item.Id);
        item.IsActive = true;
        item.StateDisplay = "Active";
    }

    [RelayCommand]
    private async Task DeactivateExtension(ExtensionListItem? item)
    {
        if (item == null) return;
        await _extensionHost.DeactivateExtensionAsync(item.Id);
        item.IsActive = false;
        item.StateDisplay = "Installed";
    }

    private void OnExtensionLoaded(LoadedExtension ext)
    {
        Services.DispatcherService.Post(() =>
        {
            var item = new ExtensionListItem
            {
                Id = ext.Manifest.Id,
                Name = ext.Manifest.DisplayName,
                Description = ext.Manifest.Description,
                Publisher = ext.Manifest.Publisher,
                Version = ext.Manifest.Version,
                Categories = string.Join(", ", ext.Manifest.Categories),
                StateDisplay = "Installed",
                IsActive = false
            };
            InstalledExtensions.Add(item);
            FilteredExtensions.Add(item);
            OnPropertyChanged(nameof(InstalledHeader));
        });
    }

    private void OnExtensionActivated(LoadedExtension ext)
    {
        Services.DispatcherService.Post(() =>
        {
            var item = InstalledExtensions.FirstOrDefault(i => i.Id == ext.Manifest.Id);
            if (item != null)
            {
                item.IsActive = true;
                item.StateDisplay = "Active";
            }
        });
    }

    private void FilterExtensions(string query)
    {
        FilteredExtensions.Clear();
        var matches = InstalledExtensions
            .Where(e => string.IsNullOrEmpty(query) ||
                        e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        e.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        e.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matches)
            FilteredExtensions.Add(item);
    }
}

/// <summary>
/// Display item for an extension in the list.
/// </summary>
public partial class ExtensionListItem : ObservableObject
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string Categories { get; init; } = "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _stateDisplay = "Installed";
}
