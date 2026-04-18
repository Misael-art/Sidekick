using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public class TriggerManager : IDisposable
{
    private readonly INodeRegistry _registry;
    private readonly FlowExecutor _executor;
    private readonly List<ActiveTrigger> _activeTriggers = new();
    private CancellationTokenSource? _cts;

    public event Action<string, string>? LogMessage;

    public TriggerManager(INodeRegistry registry, FlowExecutor executor)
    {
        _registry = registry;
        _executor = executor;
    }

    public async Task ActivateFlowTriggersAsync(Flow flow)
    {
        _cts = new CancellationTokenSource();

        var triggerInstances = flow.Nodes
            .Where(n => _registry.GetDefinition(n.TypeId)?.Category == NodeCategory.Trigger);

        foreach (var instance in triggerInstances)
        {
            var node = _registry.CreateInstance(instance.TypeId);
            if (node is not ITriggerNode trigger) continue;

            node.Id = instance.Id;
            node.Configure(instance.Properties);

            trigger.Triggered += args => OnTriggerFired(flow, instance.Id, args);

            await trigger.StartWatchingAsync(_cts.Token);

            _activeTriggers.Add(new ActiveTrigger
            {
                FlowId = flow.Id,
                NodeId = instance.Id,
                Trigger = trigger
            });

            LogMessage?.Invoke(instance.Id, $"Trigger activated: {node.Definition.DisplayName}");
        }
    }

    public async Task DeactivateAllAsync()
    {
        _cts?.Cancel();

        foreach (var active in _activeTriggers)
        {
            try { await active.Trigger.StopWatchingAsync(); }
            catch { /* ignore cleanup errors */ }
        }

        _activeTriggers.Clear();
    }

    public async Task DeactivateFlowAsync(string flowId)
    {
        var flowTriggers = _activeTriggers.Where(t => t.FlowId == flowId).ToList();

        foreach (var active in flowTriggers)
        {
            try { await active.Trigger.StopWatchingAsync(); }
            catch { /* ignore cleanup errors */ }
            _activeTriggers.Remove(active);
        }
    }

    private async void OnTriggerFired(Flow flow, string triggerNodeId, TriggerEventArgs args)
    {
        try
        {
            LogMessage?.Invoke(triggerNodeId, "Trigger fired");
            await _executor.ExecuteFromTriggerAsync(
                flow, triggerNodeId, args.Data, _cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(triggerNodeId, $"Trigger execution error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        foreach (var active in _activeTriggers)
        {
            try { active.Trigger.StopWatchingAsync().Wait(TimeSpan.FromSeconds(2)); }
            catch { /* ignore */ }
        }
        _activeTriggers.Clear();
    }

    private class ActiveTrigger
    {
        public required string FlowId { get; init; }
        public required string NodeId { get; init; }
        public required ITriggerNode Trigger { get; init; }
    }
}
