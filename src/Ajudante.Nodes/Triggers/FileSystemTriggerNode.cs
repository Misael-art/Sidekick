using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.FileSystem;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.filesystem",
    DisplayName = "File System Trigger",
    Category = NodeCategory.Trigger,
    Description = "Fires when a file is created, changed, or deleted in a watched folder",
    Color = "#EF4444")]
public class FileSystemTriggerNode : ITriggerNode, IDisposable
{
    private readonly FileWatcher _fileWatcher = new();
    private string _path = "";
    private string _filter = "*.*";
    private string _eventType = "Created";
    private bool _watching;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.filesystem",
        DisplayName = "File System Trigger",
        Category = NodeCategory.Trigger,
        Description = "Fires when a file is created, changed, or deleted in a watched folder",
        Color = "#EF4444",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String },
            new() { Id = "fileName", Name = "File Name", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "path",
                Name = "Watch Path",
                Type = PropertyType.FolderPath,
                DefaultValue = "",
                Description = "The folder to watch for file changes"
            },
            new()
            {
                Id = "filter",
                Name = "File Filter",
                Type = PropertyType.String,
                DefaultValue = "*.*",
                Description = "File filter pattern (e.g., *.txt, *.csv)"
            },
            new()
            {
                Id = "eventType",
                Name = "Event Type",
                Type = PropertyType.Dropdown,
                DefaultValue = "Created",
                Description = "The type of file event to listen for",
                Options = new[] { "Created", "Changed", "Deleted" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("path", out var p) && p is string path)
            _path = path;
        if (properties.TryGetValue("filter", out var f) && f is string filter)
            _filter = filter;
        if (properties.TryGetValue("eventType", out var et) && et is string eventType)
            _eventType = eventType;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_watching)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_path))
            return Task.CompletedTask;

        _watching = true;

        switch (_eventType)
        {
            case "Created":
                _fileWatcher.FileCreated += OnFileEvent;
                break;
            case "Changed":
                _fileWatcher.FileChanged += OnFileEvent;
                break;
            case "Deleted":
                _fileWatcher.FileDeleted += OnFileEvent;
                break;
        }

        _fileWatcher.Watch(_path, _filter);
        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        if (_watching)
        {
            _fileWatcher.StopWatching();
            _fileWatcher.FileCreated -= OnFileEvent;
            _fileWatcher.FileChanged -= OnFileEvent;
            _fileWatcher.FileDeleted -= OnFileEvent;
            _watching = false;
        }

        return Task.CompletedTask;
    }

    private void OnFileEvent(object? sender, FileWatchEventArgs e)
    {
        Triggered?.Invoke(new TriggerEventArgs
        {
            Data = new Dictionary<string, object?>
            {
                ["filePath"] = e.FullPath,
                ["fileName"] = e.FileName,
                ["eventType"] = _eventType
            },
            Timestamp = DateTime.UtcNow
        });
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
        _fileWatcher.Dispose();
    }
}
