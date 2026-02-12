using Avalonia.Threading;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Helper for marshalling actions to the UI thread.
/// </summary>
public static class DispatcherService
{
    /// <summary>
    /// Post an action to the UI thread.
    /// </summary>
    public static void Post(Action action)
    {
        Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Invoke an action on the UI thread and wait for it to complete.
    /// </summary>
    public static async Task InvokeAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Invoke a func on the UI thread and return the result.
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<T> func)
    {
        return await Dispatcher.UIThread.InvokeAsync(func, DispatcherPriority.Normal);
    }
}
