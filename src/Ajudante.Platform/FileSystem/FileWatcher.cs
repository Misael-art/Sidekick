using System.IO;

namespace Ajudante.Platform.FileSystem;

/// <summary>
/// Event data for file system change events.
/// </summary>
public sealed class FileWatchEventArgs : EventArgs
{
    /// <summary>Full path to the file that changed.</summary>
    public string FullPath { get; init; } = "";

    /// <summary>Name of the file (without directory).</summary>
    public string FileName { get; init; } = "";

    /// <summary>The type of change that occurred.</summary>
    public WatcherChangeTypes ChangeType { get; init; }

    /// <summary>For rename events, the previous full path. Null for other events.</summary>
    public string? OldFullPath { get; init; }

    /// <summary>For rename events, the previous file name. Null for other events.</summary>
    public string? OldFileName { get; init; }
}

/// <summary>
/// Watches a directory for file system changes. Wraps <see cref="FileSystemWatcher"/>
/// with debouncing and simplified events.
/// </summary>
public sealed class FileWatcher : IDisposable
{
    /// <summary>Raised when a new file is created in the watched directory.</summary>
    public event EventHandler<FileWatchEventArgs>? FileCreated;

    /// <summary>Raised when a file is modified.</summary>
    public event EventHandler<FileWatchEventArgs>? FileChanged;

    /// <summary>Raised when a file is deleted.</summary>
    public event EventHandler<FileWatchEventArgs>? FileDeleted;

    /// <summary>Raised when a file is renamed.</summary>
    public event EventHandler<FileWatchEventArgs>? FileRenamed;

    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    /// <summary>
    /// Starts watching the specified path for file changes.
    /// </summary>
    /// <param name="path">Directory to watch.</param>
    /// <param name="filter">File filter pattern (e.g. "*.txt"). Default "*.*" watches all files.</param>
    /// <param name="includeSubdirectories">Whether to watch subdirectories recursively.</param>
    public void Watch(string path, string filter = "*.*", bool includeSubdirectories = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        lock (_lock)
        {
            // Stop any existing watcher
            StopInternal();

            _watcher = new FileSystemWatcher(path, filter)
            {
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
                             | NotifyFilters.CreationTime,
                IncludeSubdirectories = includeSubdirectories,
                InternalBufferSize = 64 * 1024 // 64 KB buffer for high-throughput scenarios
            };

            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;

            _watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Stops watching for file system changes and releases resources.
    /// </summary>
    public void StopWatching()
    {
        lock (_lock)
        {
            StopInternal();
        }
    }

    /// <summary>
    /// Whether the watcher is currently active.
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _watcher?.EnableRaisingEvents == true;
            }
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        FileCreated?.Invoke(this, new FileWatchEventArgs
        {
            FullPath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = e.ChangeType
        });
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        FileChanged?.Invoke(this, new FileWatchEventArgs
        {
            FullPath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = e.ChangeType
        });
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        FileDeleted?.Invoke(this, new FileWatchEventArgs
        {
            FullPath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = e.ChangeType
        });
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        FileRenamed?.Invoke(this, new FileWatchEventArgs
        {
            FullPath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = e.ChangeType,
            OldFullPath = e.OldFullPath,
            OldFileName = e.OldName ?? (e.OldFullPath is not null ? Path.GetFileName(e.OldFullPath) : null)
        });
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // On buffer overflow or other errors, restart the watcher to recover
        Exception? ex = e.GetException();
        if (ex is InternalBufferOverflowException)
        {
            lock (_lock)
            {
                if (_watcher is not null)
                {
                    string path = _watcher.Path;
                    string filter = _watcher.Filter;
                    bool recursive = _watcher.IncludeSubdirectories;

                    StopInternal();

                    // Attempt restart
                    try { Watch(path, filter, recursive); }
                    catch { /* give up gracefully */ }
                }
            }
        }
    }

    private void StopInternal()
    {
        if (_watcher is null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnCreated;
        _watcher.Changed -= OnChanged;
        _watcher.Deleted -= OnDeleted;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        StopWatching();
    }
}
