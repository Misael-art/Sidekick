using System.Diagnostics;
using System.IO;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.processEvent",
    DisplayName = "Process Event",
    Category = NodeCategory.Trigger,
    Description = "Fires when a matching Windows process starts or stops",
    Color = "#EF4444")]
public class ProcessEventTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private HashSet<int> _knownProcessIds = [];

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.processEvent",
        DisplayName = "Process Event",
        Category = NodeCategory.Trigger,
        Description = "Fires when a matching Windows process starts or stops",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "processId", Name = "Process ID", DataType = PortDataType.Number },
            new() { Id = "processName", Name = "Process Name", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "eventType", Name = "Event Type", Type = PropertyType.Dropdown, DefaultValue = "started", Description = "Process event to watch", Options = new[] { "started", "stopped" } },
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "", Description = "Process name, with or without .exe" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Optional full executable path" },
            new() { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "Delay between process snapshots" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_cts != null)
            return Task.CompletedTask;

        var intervalMs = Math.Max(250, NodeValueHelper.GetInt(_properties, "intervalMs", 1000));
        var eventType = NodeValueHelper.GetString(_properties, "eventType", "started");
        var processName = NormalizeProcessName(NodeValueHelper.GetString(_properties, "processName"));
        var processPath = NodeValueHelper.GetString(_properties, "processPath");

        _knownProcessIds = Snapshot(processName, processPath).Select(process => process.Id).ToHashSet();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var current = Snapshot(processName, processPath);
                    var currentIds = current.Select(process => process.Id).ToHashSet();

                    if (string.Equals(eventType, "stopped", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var stoppedId in _knownProcessIds.Except(currentIds).ToArray())
                            Fire(stoppedId, processName, "stopped");
                    }
                    else
                    {
                        foreach (var process in current.Where(process => !_knownProcessIds.Contains(process.Id)))
                            Fire(process.Id, process.Name, "started");
                    }

                    _knownProcessIds = currentIds;
                    await Task.Delay(intervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(intervalMs, token);
                }
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _knownProcessIds = [];
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
    }

    private void Fire(int processId, string processName, string eventType)
    {
        Triggered?.Invoke(new TriggerEventArgs
        {
            Data = new Dictionary<string, object?>
            {
                ["processId"] = processId,
                ["processName"] = processName,
                ["eventType"] = eventType
            },
            Timestamp = DateTime.UtcNow
        });
    }

    private static List<ProcessSnapshot> Snapshot(string processName, string processPath)
    {
        var matches = new List<ProcessSnapshot>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(processName)
                    && !string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    process.Dispose();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    var actualPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(actualPath)
                        || !string.Equals(Path.GetFullPath(actualPath), Path.GetFullPath(processPath), StringComparison.OrdinalIgnoreCase))
                    {
                        process.Dispose();
                        continue;
                    }
                }

                matches.Add(new ProcessSnapshot(process.Id, process.ProcessName));
                process.Dispose();
            }
            catch
            {
                process.Dispose();
            }
        }

        return matches;
    }

    private static string NormalizeProcessName(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }

    private sealed record ProcessSnapshot(int Id, string Name);
}
