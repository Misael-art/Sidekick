using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.desktopElementAppeared",
    DisplayName = "Desktop Element Appeared",
    Category = NodeCategory.Trigger,
    Description = "Fires when a Windows desktop element appears, with debounce, cooldown, and max repeat guards",
    Color = "#EF4444")]
public class DesktopElementAppearedTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pollCts;
    private bool _wasVisible;
    private long _lastFiredTick;
    private int _repeatCount;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.desktopElementAppeared",
        DisplayName = "Desktop Element Appeared",
        Category = NodeCategory.Trigger,
        Description = "Fires when a Windows desktop element appears, with debounce, cooldown, and max repeat guards",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "elementName", Name = "Element Name", DataType = PortDataType.String },
            new() { Id = "automationId", Name = "Automation ID", DataType = PortDataType.String },
            new() { Id = "controlType", Name = "Control Type", DataType = PortDataType.String },
            new() { Id = "processName", Name = "Process Name", DataType = PortDataType.String }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions("button")
            .Concat(new[]
            {
                new PropertyDefinition { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "How often to scan for the element" },
                new PropertyDefinition { Id = "cooldownMs", Name = "Cooldown (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Minimum time between trigger fires" },
                new PropertyDefinition { Id = "debounceMs", Name = "Debounce (ms)", Type = PropertyType.Integer, DefaultValue = 500, Description = "Element must remain present for this long before firing" },
                new PropertyDefinition { Id = "maxRepeat", Name = "Max Repeat", Type = PropertyType.Integer, DefaultValue = 20, Description = "Maximum fires for this armed session; 0 means unlimited" }
            })
            .ToList()
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
        if (_pollCts != null)
            return Task.CompletedTask;

        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _pollCts.Token;
        var intervalMs = Math.Max(100, NodeValueHelper.GetInt(_properties, "intervalMs", 1000));
        var debounceMs = Math.Max(0, NodeValueHelper.GetInt(_properties, "debounceMs", 500));
        var cooldownMs = Math.Max(0, NodeValueHelper.GetInt(_properties, "cooldownMs", 5000));
        var maxRepeat = Math.Max(0, NodeValueHelper.GetInt(_properties, "maxRepeat", 20));

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var selector = BuildSelectorForTrigger();
                    var element = BrowserSelectorHelper.FindElement(selector);
                    if (element is not null && !_wasVisible)
                    {
                        if (debounceMs > 0)
                        {
                            await Task.Delay(debounceMs, token);
                            element = BrowserSelectorHelper.FindElement(selector);
                        }

                        if (element is not null && CanFire(cooldownMs, maxRepeat))
                        {
                            _repeatCount++;
                            _lastFiredTick = Environment.TickCount64;
                            Triggered?.Invoke(new TriggerEventArgs
                            {
                                Data = BrowserSelectorHelper.BuildSelectorOutputs(element)
                                    .Concat(new Dictionary<string, object?>
                                    {
                                        ["processName"] = selector.processName,
                                        ["repeatCount"] = _repeatCount
                                    })
                                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }

                    _wasVisible = element is not null;
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
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
        _wasVisible = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
    }

    private DesktopSelector BuildSelectorForTrigger()
    {
        var flow = new Flow();
        var context = new FlowExecutionContext(flow, _pollCts?.Token ?? CancellationToken.None);
        return BrowserSelectorHelper.ResolveSelector(context, _properties);
    }

    private bool CanFire(int cooldownMs, int maxRepeat)
    {
        if (maxRepeat > 0 && _repeatCount >= maxRepeat)
            return false;

        if (cooldownMs <= 0 || _lastFiredTick <= 0)
            return true;

        return Environment.TickCount64 - _lastFiredTick >= cooldownMs;
    }
}
