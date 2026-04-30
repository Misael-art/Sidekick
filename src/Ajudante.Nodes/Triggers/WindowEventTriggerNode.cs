using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Windows;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.windowEvent",
    DisplayName = "Window Event",
    Category = NodeCategory.Trigger,
    Description = "Fires when a window is opened, closed, or focused",
    Color = "#EF4444")]
public class WindowEventTriggerNode : ITriggerNode, IDisposable
{
    private readonly WindowWatcher _windowWatcher = new();
    private string _eventType = "Opened";
    private string _windowTitle = "";
    private bool _watching;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.windowEvent",
        DisplayName = "Window Event",
        Category = NodeCategory.Trigger,
        Description = "Fires when a window is opened, closed, or focused",
        Color = "#EF4444",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "windowTitle", Name = "Window Title", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "eventType",
                Name = "Event Type",
                Type = PropertyType.Dropdown,
                DefaultValue = "Opened",
                Description = "The type of window event to listen for",
                Options = new[] { "Opened", "Closed", "Focused" }
            },
            new()
            {
                Id = "windowTitle",
                Name = "Window Title Filter",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "Optional: only trigger for windows matching this title (leave empty for all)"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("eventType", out var et) && et is string eventType)
            _eventType = eventType;
        if (properties.TryGetValue("windowTitle", out var wt) && wt is string title)
            _windowTitle = title;
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

        _watching = true;

        switch (_eventType)
        {
            case "Opened":
                _windowWatcher.WindowOpened += OnWindowEvent;
                break;
            case "Closed":
                _windowWatcher.WindowClosed += OnWindowEvent;
                break;
            case "Focused":
                _windowWatcher.WindowFocused += OnWindowEvent;
                break;
        }

        _windowWatcher.Start();
        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        if (_watching)
        {
            _windowWatcher.Stop();
            _windowWatcher.WindowOpened -= OnWindowEvent;
            _windowWatcher.WindowClosed -= OnWindowEvent;
            _windowWatcher.WindowFocused -= OnWindowEvent;
            _watching = false;
        }

        return Task.CompletedTask;
    }

    private void OnWindowEvent(object? sender, WindowEventArgs e)
    {
        if (!string.IsNullOrEmpty(_windowTitle) &&
            !e.WindowTitle.Contains(_windowTitle, StringComparison.OrdinalIgnoreCase))
            return;

        Triggered?.Invoke(new TriggerEventArgs
        {
            Data = new Dictionary<string, object?>
            {
                ["windowTitle"] = e.WindowTitle,
                ["processName"] = e.ProcessName,
                ["processId"] = e.ProcessId,
                ["eventType"] = _eventType
            },
            Timestamp = DateTime.UtcNow
        });
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
        _windowWatcher.Dispose();
    }
}
