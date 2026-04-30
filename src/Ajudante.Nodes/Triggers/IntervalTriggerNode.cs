using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.interval",
    DisplayName = "Interval",
    Category = NodeCategory.Trigger,
    Description = "Fires repeatedly after a fixed interval",
    Color = "#EF4444")]
public class IntervalTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private int _fireCount;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.interval",
        DisplayName = "Interval",
        Category = NodeCategory.Trigger,
        Description = "Fires repeatedly after a fixed interval",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "fireCount", Name = "Fire Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "intervalMs", Name = "Interval (ms)", Type = PropertyType.Integer, DefaultValue = 60000, Description = "Delay between fires" },
            new() { Id = "fireImmediately", Name = "Fire Immediately", Type = PropertyType.Boolean, DefaultValue = false, Description = "Fire once as soon as the flow is armed" },
            new() { Id = "maxRepeat", Name = "Max Repeat", Type = PropertyType.Integer, DefaultValue = 0, Description = "Maximum fires for this armed session; 0 means unlimited" }
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

        var intervalMs = Math.Max(250, NodeValueHelper.GetInt(_properties, "intervalMs", 60000));
        var fireImmediately = NodeValueHelper.GetBool(_properties, "fireImmediately");
        var maxRepeat = Math.Max(0, NodeValueHelper.GetInt(_properties, "maxRepeat", 0));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            if (!fireImmediately)
                await Task.Delay(intervalMs, token);

            while (!token.IsCancellationRequested)
            {
                if (maxRepeat > 0 && _fireCount >= maxRepeat)
                    break;

                _fireCount++;
                Triggered?.Invoke(new TriggerEventArgs
                {
                    Data = new Dictionary<string, object?>
                    {
                        ["fireCount"] = _fireCount
                    },
                    Timestamp = DateTime.UtcNow
                });

                await Task.Delay(intervalMs, token);
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _fireCount = 0;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
    }
}
