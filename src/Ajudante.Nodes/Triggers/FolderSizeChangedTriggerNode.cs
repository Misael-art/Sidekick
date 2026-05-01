using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.folderSizeChanged",
    DisplayName = "Folder Size Changed",
    Category = NodeCategory.Trigger,
    Description = "Fires when a folder grows or shrinks beyond a threshold",
    Color = "#EF4444")]
public sealed class FolderSizeChangedTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _props = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private long? _lastSize;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.folderSizeChanged",
        DisplayName = "Folder Size Changed",
        Category = NodeCategory.Trigger,
        Description = "Polls folder size and fires when it changes by more than a threshold.",
        Color = "#EF4444",
        OutputPorts = new()
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "currentSize", Name = "Current Size", DataType = PortDataType.Number },
            new() { Id = "previousSize", Name = "Previous Size", DataType = PortDataType.Number },
            new() { Id = "deltaBytes", Name = "Delta Bytes", DataType = PortDataType.Number }
        },
        Properties = new()
        {
            new() { Id = "folder", Name = "Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "recursive", Name = "Recursive", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "thresholdBytes", Name = "Threshold (bytes)", Type = PropertyType.Integer, DefaultValue = 1048576, Description = "Min absolute change to fire" },
            new() { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 5000 },
            new() { Id = "direction", Name = "Direction", Type = PropertyType.Dropdown, DefaultValue = "any", Options = new[] { "any", "growth", "shrink" } }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties, StringComparer.OrdinalIgnoreCase);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) =>
        Task.FromResult(NodeResult.Ok("triggered"));

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_cts != null) return Task.CompletedTask;

        var folder = NodeValueHelper.GetString(_props, "folder");
        var recursive = NodeValueHelper.GetBool(_props, "recursive", true);
        var threshold = Math.Max(0, NodeValueHelper.GetInt(_props, "thresholdBytes", 1048576));
        var interval = Math.Max(500, NodeValueHelper.GetInt(_props, "intervalMs", 5000));
        var direction = NodeValueHelper.GetString(_props, "direction", "any").ToLowerInvariant();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        var size = ComputeSize(folder, recursive);
                        if (_lastSize.HasValue)
                        {
                            var delta = size - _lastSize.Value;
                            var should = direction switch
                            {
                                "growth" => delta >= threshold,
                                "shrink" => -delta >= threshold,
                                _ => Math.Abs(delta) >= threshold
                            };
                            if (should)
                            {
                                Triggered?.Invoke(new TriggerEventArgs
                                {
                                    Data = new()
                                    {
                                        ["currentSize"] = size,
                                        ["previousSize"] = _lastSize.Value,
                                        ["deltaBytes"] = delta
                                    },
                                    Timestamp = DateTime.UtcNow
                                });
                                _lastSize = size;
                            }
                        }
                        else
                        {
                            _lastSize = size;
                        }
                    }
                }
                catch
                {
                    // ignore transient errors
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
        _lastSize = null;
        return Task.CompletedTask;
    }

    public void Dispose() => StopWatchingAsync().GetAwaiter().GetResult();

    private static long ComputeSize(string folder, bool recursive)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        long total = 0;
        foreach (var f in Directory.EnumerateFiles(folder, "*", option))
        {
            try { total += new FileInfo(f).Length; } catch { }
        }
        return total;
    }
}
