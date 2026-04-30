using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.desktopElementTextChanged",
    DisplayName = "Desktop Element Text Changed",
    Category = NodeCategory.Trigger,
    Description = "Fires when a Windows desktop element's readable text changes",
    Color = "#EF4444")]
public class DesktopElementTextChangedTriggerNode : ITriggerNode, IDisposable
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pollCts;
    private string? _lastText;
    private long _lastFiredTick;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.desktopElementTextChanged",
        DisplayName = "Desktop Element Text Changed",
        Category = NodeCategory.Trigger,
        Description = "Fires when a Windows desktop element's readable text changes",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "oldText", Name = "Old Text", DataType = PortDataType.String },
            new() { Id = "newText", Name = "New Text", DataType = PortDataType.String }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions()
            .Concat(new[]
            {
                new PropertyDefinition { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "How often to read the element" },
                new PropertyDefinition { Id = "cooldownMs", Name = "Cooldown (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "Minimum time between trigger fires" },
                new PropertyDefinition { Id = "fireInitial", Name = "Fire Initial", Type = PropertyType.Boolean, DefaultValue = false, Description = "Fire once when the first text value is read" }
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
        var cooldownMs = Math.Max(0, NodeValueHelper.GetInt(_properties, "cooldownMs", 1000));
        var fireInitial = NodeValueHelper.GetBool(_properties, "fireInitial");

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var selector = BuildSelectorForTrigger();
                    var element = BrowserSelectorHelper.FindElement(selector);
                    if (element is not null)
                    {
                        var newText = AutomationElementLocator.ExtractText(element);
                        var isInitial = _lastText is null;
                        var changed = !string.Equals(_lastText, newText, StringComparison.Ordinal);
                        if ((fireInitial && isInitial) || (!isInitial && changed))
                        {
                            if (CanFire(cooldownMs))
                            {
                                Triggered?.Invoke(new TriggerEventArgs
                                {
                                    Data = new Dictionary<string, object?>
                                    {
                                        ["oldText"] = _lastText ?? "",
                                        ["newText"] = newText
                                    },
                                    Timestamp = DateTime.UtcNow
                                });
                                _lastFiredTick = Environment.TickCount64;
                            }
                        }

                        _lastText = newText;
                    }

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
        _lastText = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
    }

    private DesktopSelector BuildSelectorForTrigger()
    {
        var context = new FlowExecutionContext(new Flow(), _pollCts?.Token ?? CancellationToken.None);
        return BrowserSelectorHelper.ResolveSelector(context, _properties);
    }

    private bool CanFire(int cooldownMs)
    {
        return cooldownMs <= 0
            || _lastFiredTick <= 0
            || Environment.TickCount64 - _lastFiredTick >= cooldownMs;
    }
}
