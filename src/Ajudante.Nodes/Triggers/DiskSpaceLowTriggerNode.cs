using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.diskSpaceLow",
    DisplayName = "Disk Space Low",
    Category = NodeCategory.Trigger,
    Description = "Fires when free space on a drive falls below a threshold",
    Color = "#EF4444")]
public sealed class DiskSpaceLowTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _props = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private bool _wasLow;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.diskSpaceLow",
        DisplayName = "Disk Space Low",
        Category = NodeCategory.Trigger,
        Description = "Polls free disk space. Fires once when crossing under the threshold (rearm on recovery).",
        Color = "#EF4444",
        OutputPorts = new()
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "drive", Name = "Drive", DataType = PortDataType.String },
            new() { Id = "freeBytes", Name = "Free Bytes", DataType = PortDataType.Number },
            new() { Id = "freePercent", Name = "Free Percent", DataType = PortDataType.Number }
        },
        Properties = new()
        {
            new() { Id = "drive", Name = "Drive (eg C:)", Type = PropertyType.String, DefaultValue = "C:" },
            new() { Id = "thresholdMb", Name = "Threshold (MB)", Type = PropertyType.Integer, DefaultValue = 1024 },
            new() { Id = "thresholdMode", Name = "Threshold Mode", Type = PropertyType.Dropdown, DefaultValue = "absolute", Options = new[] { "absolute", "percent" } },
            new() { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 30000 }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties, StringComparer.OrdinalIgnoreCase);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) =>
        Task.FromResult(NodeResult.Ok("triggered"));

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_cts != null) return Task.CompletedTask;

        var driveLetter = NodeValueHelper.GetString(_props, "drive", "C:");
        var thresholdMb = Math.Max(0, NodeValueHelper.GetInt(_props, "thresholdMb", 1024));
        var thresholdMode = NodeValueHelper.GetString(_props, "thresholdMode", "absolute").ToLowerInvariant();
        var interval = Math.Max(1000, NodeValueHelper.GetInt(_props, "intervalMs", 30000));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var di = new DriveInfo(driveLetter.TrimEnd(':') + ":");
                    if (di.IsReady)
                    {
                        var freeBytes = di.AvailableFreeSpace;
                        var totalBytes = di.TotalSize;
                        double percentFree = totalBytes > 0 ? (double)freeBytes / totalBytes * 100.0 : 0;

                        var isLow = thresholdMode == "percent"
                            ? percentFree < thresholdMb
                            : freeBytes < (long)thresholdMb * 1024 * 1024;

                        if (isLow && !_wasLow)
                        {
                            Triggered?.Invoke(new TriggerEventArgs
                            {
                                Data = new()
                                {
                                    ["drive"] = di.Name,
                                    ["freeBytes"] = freeBytes,
                                    ["freePercent"] = percentFree
                                },
                                Timestamp = DateTime.UtcNow
                            });
                        }
                        _wasLow = isLow;
                    }
                }
                catch
                {
                    // skip transient errors
                }
                try { await Task.Delay(interval, token); } catch (OperationCanceledException) { break; }
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _wasLow = false;
        return Task.CompletedTask;
    }

    public void Dispose() => StopWatchingAsync().GetAwaiter().GetResult();
}
