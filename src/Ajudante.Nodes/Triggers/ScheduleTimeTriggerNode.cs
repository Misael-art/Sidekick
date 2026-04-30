using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.scheduleTime",
    DisplayName = "Schedule Time",
    Category = NodeCategory.Trigger,
    Description = "Fires once per day at a fixed local time",
    Color = "#EF4444")]
public class ScheduleTimeTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private DateOnly? _lastFiredDate;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.scheduleTime",
        DisplayName = "Schedule Time",
        Category = NodeCategory.Trigger,
        Description = "Fires once per day at a fixed local time",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "scheduledFor", Name = "Scheduled For", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "timeOfDay", Name = "Time Of Day", Type = PropertyType.String, DefaultValue = "09:00", Description = "Local time in HH:mm format" },
            new() { Id = "pollIntervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "How often to check the clock" }
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

        if (!TimeOnly.TryParse(NodeValueHelper.GetString(_properties, "timeOfDay", "09:00"), out var targetTime))
            targetTime = new TimeOnly(9, 0);

        var pollIntervalMs = Math.Max(250, NodeValueHelper.GetInt(_properties, "pollIntervalMs", 1000));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var today = DateOnly.FromDateTime(now);
                    if (_lastFiredDate != today && TimeOnly.FromDateTime(now) >= targetTime)
                    {
                        _lastFiredDate = today;
                        Triggered?.Invoke(new TriggerEventArgs
                        {
                            Data = new Dictionary<string, object?>
                            {
                                ["scheduledFor"] = now.Date.Add(targetTime.ToTimeSpan()).ToString("O")
                            },
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    await Task.Delay(pollIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
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
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
    }
}
