namespace MiniClaudeCode.Avalonia.Extensions;

/// <summary>
/// Extension lifecycle interface. Every extension must implement this.
/// Mirrors VS Code's activate/deactivate pattern.
/// </summary>
public interface IExtension
{
    /// <summary>Called when the extension is activated.</summary>
    Task ActivateAsync(IExtensionContext context);

    /// <summary>Called when the extension is deactivated (cleanup).</summary>
    Task DeactivateAsync();
}

/// <summary>
/// Context provided to extensions during activation.
/// Mirrors VS Code's ExtensionContext.
/// </summary>
public interface IExtensionContext
{
    /// <summary>The absolute path of the extension folder.</summary>
    string ExtensionPath { get; }

    /// <summary>The extension's storage path for persistent data.</summary>
    string StoragePath { get; }

    /// <summary>The extension's global storage path.</summary>
    string GlobalStoragePath { get; }

    /// <summary>The manifest of this extension.</summary>
    ExtensionManifest Manifest { get; }

    /// <summary>Register a command handler.</summary>
    void RegisterCommand(string commandId, Func<object?, Task> handler);

    /// <summary>Register a disposable to clean up on deactivation.</summary>
    void RegisterDisposable(IDisposable disposable);

    /// <summary>Subscribe to an event.</summary>
    void SubscribeEvent(string eventName, Action<object?> handler);

    /// <summary>Log a message.</summary>
    void Log(string message);

    /// <summary>Show an information notification.</summary>
    void ShowInfo(string message);

    /// <summary>Show a warning notification.</summary>
    void ShowWarning(string message);

    /// <summary>Show an error notification.</summary>
    void ShowError(string message);
}
