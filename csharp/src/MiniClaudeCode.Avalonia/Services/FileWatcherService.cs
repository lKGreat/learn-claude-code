namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Service that watches workspace file system for changes and notifies subscribers.
/// Wraps FileSystemWatcher with debouncing to avoid excessive refresh events.
/// </summary>
public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounceTimer;
    private readonly HashSet<string> _pendingChanges = [];
    private readonly object _lock = new();

    /// <summary>Fired when files/directories change (debounced).</summary>
    public event Action<IReadOnlyList<string>>? FilesChanged;

    /// <summary>Fired when a specific file is created.</summary>
    public event Action<string>? FileCreated;

    /// <summary>Fired when a specific file is deleted.</summary>
    public event Action<string>? FileDeleted;

    /// <summary>Fired when a specific file is renamed.</summary>
    public event Action<string, string>? FileRenamed;

    public FileWatcherService()
    {
        _debounceTimer = new System.Timers.Timer(300); // 300ms debounce
        _debounceTimer.Elapsed += OnDebounceElapsed;
        _debounceTimer.AutoReset = false;
    }

    /// <summary>
    /// Start watching a directory for changes.
    /// </summary>
    public void Watch(string path)
    {
        Stop();

        if (!Directory.Exists(path)) return;

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileSystemChanged;
        _watcher.Created += OnFileSystemCreated;
        _watcher.Deleted += OnFileSystemDeleted;
        _watcher.Renamed += OnFileSystemRenamed;
        _watcher.Error += OnWatcherError;
    }

    /// <summary>
    /// Stop watching.
    /// </summary>
    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;

        lock (_lock)
        {
            _pendingChanges.Add(e.FullPath);
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnFileSystemCreated(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;
        DispatcherService.Post(() => FileCreated?.Invoke(e.FullPath));

        lock (_lock) { _pendingChanges.Add(e.FullPath); }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnFileSystemDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;
        DispatcherService.Post(() => FileDeleted?.Invoke(e.FullPath));

        lock (_lock) { _pendingChanges.Add(e.FullPath); }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;
        DispatcherService.Post(() => FileRenamed?.Invoke(e.OldFullPath, e.FullPath));

        lock (_lock) { _pendingChanges.Add(e.FullPath); }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        List<string> changes;
        lock (_lock)
        {
            changes = [.. _pendingChanges];
            _pendingChanges.Clear();
        }

        if (changes.Count > 0)
        {
            DispatcherService.Post(() => FilesChanged?.Invoke(changes));
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Recreate watcher on error
        var path = _watcher?.Path;
        if (path != null)
        {
            Stop();
            Watch(path);
        }
    }

    private static bool ShouldIgnore(string path)
    {
        // Ignore common noise directories
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s is ".git" or "node_modules" or "bin" or "obj" or
            ".vs" or ".idea" or "__pycache__" or ".cache");
    }

    public void Dispose()
    {
        Stop();
        _debounceTimer.Dispose();
    }
}
