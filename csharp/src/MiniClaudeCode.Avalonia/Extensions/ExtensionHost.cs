using System.Reflection;
using System.Text.Json;

namespace MiniClaudeCode.Avalonia.Extensions;

/// <summary>
/// Extension host that manages the lifecycle of all extensions.
/// Mirrors VS Code's ExtensionHostMain running in a separate process,
/// but simplified to run in the same process for v1.
/// </summary>
public class ExtensionHost
{
    private readonly List<LoadedExtension> _loadedExtensions = [];
    private readonly Dictionary<string, Func<object?, Task>> _commandRegistry = [];
    private readonly Dictionary<string, List<Action<object?>>> _eventSubscriptions = [];
    private readonly string _extensionsDir;

    /// <summary>All loaded extensions.</summary>
    public IReadOnlyList<LoadedExtension> LoadedExtensions => _loadedExtensions.AsReadOnly();

    /// <summary>Fired when an extension is loaded.</summary>
    public event Action<LoadedExtension>? ExtensionLoaded;

    /// <summary>Fired when an extension is activated.</summary>
    public event Action<LoadedExtension>? ExtensionActivated;

    /// <summary>Notification callback for extensions.</summary>
    public Action<string, string>? NotificationCallback { get; set; }

    public ExtensionHost(string? extensionsDir = null)
    {
        _extensionsDir = extensionsDir ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MiniClaudeCode", "extensions");
    }

    /// <summary>
    /// Discover and load all extensions from the extensions directory.
    /// </summary>
    public async Task DiscoverExtensionsAsync()
    {
        if (!Directory.Exists(_extensionsDir))
        {
            Directory.CreateDirectory(_extensionsDir);
            return;
        }

        foreach (var extDir in Directory.GetDirectories(_extensionsDir))
        {
            try
            {
                await LoadExtensionFromDirectoryAsync(extDir);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load extension from {extDir}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Load a single extension from its directory.
    /// </summary>
    public async Task LoadExtensionFromDirectoryAsync(string extDir)
    {
        var manifestPath = Path.Combine(extDir, "extension.json");
        if (!File.Exists(manifestPath)) return;

        var json = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest == null) return;

        var loaded = new LoadedExtension
        {
            Manifest = manifest,
            ExtensionPath = extDir,
            State = ExtensionState.Loaded
        };

        _loadedExtensions.Add(loaded);
        ExtensionLoaded?.Invoke(loaded);
    }

    /// <summary>
    /// Activate a specific extension by its ID.
    /// </summary>
    public async Task ActivateExtensionAsync(string extensionId)
    {
        var ext = _loadedExtensions.FirstOrDefault(e => e.Manifest.Id == extensionId);
        if (ext == null || ext.State == ExtensionState.Active) return;

        try
        {
            // Load the assembly
            var mainPath = Path.Combine(ext.ExtensionPath, ext.Manifest.Main);
            if (!File.Exists(mainPath)) return;

            var assembly = Assembly.LoadFrom(mainPath);
            var extensionType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsInterface);

            if (extensionType == null) return;

            ext.Instance = (IExtension?)Activator.CreateInstance(extensionType);
            if (ext.Instance == null) return;

            // Pre-register contributed commands as placeholders BEFORE activation
            // so that extensions can override them during ActivateAsync
            foreach (var cmd in ext.Manifest.Contributes.Commands)
            {
                if (!_commandRegistry.ContainsKey(cmd.Command))
                {
                    _commandRegistry[cmd.Command] = _ => Task.CompletedTask;
                }
            }

            // Create context
            var context = new ExtensionContextImpl(
                ext.ExtensionPath,
                Path.Combine(_extensionsDir, ".storage", ext.Manifest.Id),
                Path.Combine(_extensionsDir, ".global-storage"),
                ext.Manifest,
                this);

            ext.Context = context;

            // Activate - the extension may override command handlers via context.RegisterCommand
            await ext.Instance.ActivateAsync(context);
            ext.State = ExtensionState.Active;

            // Process contribution points
            ProcessContributions(ext);

            ExtensionActivated?.Invoke(ext);
        }
        catch (Exception ex)
        {
            ext.State = ExtensionState.Error;
            ext.ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Try to activate extensions based on an activation event.
    /// Extensions with matching activation events will be activated lazily.
    /// </summary>
    public async Task FireActivationEventAsync(string eventType, string? eventArg = null)
    {
        var eventStr = eventArg != null ? $"{eventType}:{eventArg}" : eventType;

        foreach (var ext in _loadedExtensions.Where(e => e.State == ExtensionState.Loaded))
        {
            var events = ext.Manifest.ActivationEvents;
            if (events.Contains("*") || events.Contains(eventStr) || events.Contains(eventType))
            {
                await ActivateExtensionAsync(ext.Manifest.Id);
            }
        }
    }

    /// <summary>
    /// Process contribution points from an activated extension.
    /// </summary>
    private void ProcessContributions(LoadedExtension ext)
    {
        var contributes = ext.Manifest.Contributes;

        // Process keybinding contributions
        foreach (var kb in contributes.Keybindings)
        {
            _keybindings[kb.Key] = kb.Command;
        }

        // Process language contributions
        foreach (var lang in contributes.Languages)
        {
            _registeredLanguages[lang.Id] = lang;
        }

        // Notify listeners of new contributions
        ContributionsChanged?.Invoke(ext.Manifest.Id);
    }

    /// <summary>Keybinding registry: key combo -> command id.</summary>
    private readonly Dictionary<string, string> _keybindings = [];

    /// <summary>Registered languages from extensions.</summary>
    private readonly Dictionary<string, LanguageContribution> _registeredLanguages = [];

    /// <summary>Fired when an extension registers new contributions.</summary>
    public event Action<string>? ContributionsChanged;

    /// <summary>Get all registered keybindings.</summary>
    public IReadOnlyDictionary<string, string> Keybindings => _keybindings;

    /// <summary>Get all registered languages.</summary>
    public IReadOnlyDictionary<string, LanguageContribution> RegisteredLanguages => _registeredLanguages;

    /// <summary>
    /// Deactivate a specific extension.
    /// </summary>
    public async Task DeactivateExtensionAsync(string extensionId)
    {
        var ext = _loadedExtensions.FirstOrDefault(e => e.Manifest.Id == extensionId);
        if (ext?.Instance == null || ext.State != ExtensionState.Active) return;

        try
        {
            await ext.Instance.DeactivateAsync();
            ext.State = ExtensionState.Loaded;
        }
        catch
        {
            ext.State = ExtensionState.Error;
        }
    }

    /// <summary>
    /// Deactivate all extensions (shutdown).
    /// </summary>
    public async Task DeactivateAllAsync()
    {
        foreach (var ext in _loadedExtensions.Where(e => e.State == ExtensionState.Active))
        {
            try
            {
                if (ext.Instance != null)
                    await ext.Instance.DeactivateAsync();
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Execute a command by ID.
    /// </summary>
    public async Task<bool> ExecuteCommandAsync(string commandId, object? args = null)
    {
        if (_commandRegistry.TryGetValue(commandId, out var handler))
        {
            await handler(args);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Fire an event to all subscribers.
    /// </summary>
    public void FireEvent(string eventName, object? data = null)
    {
        if (_eventSubscriptions.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try { handler(data); }
                catch { /* ignore */ }
            }
        }
    }

    internal void RegisterCommand(string commandId, Func<object?, Task> handler)
    {
        _commandRegistry[commandId] = handler;
    }

    internal void SubscribeEvent(string eventName, Action<object?> handler)
    {
        if (!_eventSubscriptions.TryGetValue(eventName, out var handlers))
        {
            handlers = [];
            _eventSubscriptions[eventName] = handlers;
        }
        handlers.Add(handler);
    }
}

/// <summary>
/// Represents a loaded extension with its runtime state.
/// </summary>
public class LoadedExtension
{
    public ExtensionManifest Manifest { get; init; } = new();
    public string ExtensionPath { get; init; } = "";
    public ExtensionState State { get; set; } = ExtensionState.Loaded;
    public IExtension? Instance { get; set; }
    public ExtensionContextImpl? Context { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ExtensionState
{
    Loaded,
    Active,
    Error,
    Disabled
}

/// <summary>
/// Implementation of IExtensionContext.
/// </summary>
public class ExtensionContextImpl : IExtensionContext
{
    private readonly ExtensionHost _host;
    private readonly List<IDisposable> _disposables = [];

    public string ExtensionPath { get; }
    public string StoragePath { get; }
    public string GlobalStoragePath { get; }
    public ExtensionManifest Manifest { get; }

    public ExtensionContextImpl(string extensionPath, string storagePath,
        string globalStoragePath, ExtensionManifest manifest, ExtensionHost host)
    {
        ExtensionPath = extensionPath;
        StoragePath = storagePath;
        GlobalStoragePath = globalStoragePath;
        Manifest = manifest;
        _host = host;

        // Ensure storage directories exist
        Directory.CreateDirectory(storagePath);
        Directory.CreateDirectory(globalStoragePath);
    }

    public void RegisterCommand(string commandId, Func<object?, Task> handler)
        => _host.RegisterCommand(commandId, handler);

    public void RegisterDisposable(IDisposable disposable)
        => _disposables.Add(disposable);

    public void SubscribeEvent(string eventName, Action<object?> handler)
        => _host.SubscribeEvent(eventName, handler);

    public void Log(string message)
        => System.Diagnostics.Debug.WriteLine($"[Extension:{Manifest.Id}] {message}");

    public void ShowInfo(string message)
        => _host.NotificationCallback?.Invoke("info", message);

    public void ShowWarning(string message)
        => _host.NotificationCallback?.Invoke("warning", message);

    public void ShowError(string message)
        => _host.NotificationCallback?.Invoke("error", message);

    internal void Dispose()
    {
        foreach (var d in _disposables)
        {
            try { d.Dispose(); }
            catch { /* ignore */ }
        }
        _disposables.Clear();
    }
}
