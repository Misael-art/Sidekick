using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public sealed class TriggerFiredEventArgs : EventArgs
{
    public required Flow Flow { get; init; }
    public required string TriggerNodeId { get; init; }
    public required Dictionary<string, object?> Data { get; init; }
    public required DateTime Timestamp { get; init; }
}

public sealed class TriggerManagerLogEventArgs : EventArgs
{
    public required string FlowId { get; init; }
    public required string NodeId { get; init; }
    public required string Message { get; init; }
}

public sealed class TriggerManagerErrorEventArgs : EventArgs
{
    public required string FlowId { get; init; }
    public required string NodeId { get; init; }
    public required string Error { get; init; }
}

public sealed class FlowTriggerActivationResult
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public bool Armed { get; init; }
    public string[] ActiveTriggerNodeIds { get; init; } = [];
    public string[] Errors { get; init; } = [];
}

public sealed class TriggerManager : IDisposable
{
    private readonly INodeRegistry _registry;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Dictionary<string, ActiveFlowRegistration> _activeFlows = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired;
    public event EventHandler<TriggerManagerLogEventArgs>? LogMessage;
    public event EventHandler<TriggerManagerErrorEventArgs>? TriggerError;

    public TriggerManager(INodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<FlowTriggerActivationResult> ActivateFlowTriggersAsync(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!string.IsNullOrWhiteSpace(flow.Id))
        {
            await DeactivateFlowAsync(flow.Id);
        }

        var flowCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        var registration = new ActiveFlowRegistration(flow, flowCts);
        var errors = new List<string>();

        foreach (var instance in flow.Nodes.Where(IsArmableTriggerNode))
        {
            try
            {
                var node = _registry.CreateInstance(instance.TypeId);
                if (node is not ITriggerNode trigger)
                {
                    continue;
                }

                node.Id = instance.Id;
                node.Configure(instance.Properties);

                Action<TriggerEventArgs> handler = args => OnTriggerFired(flow, instance.Id, args);
                trigger.Triggered += handler;

                try
                {
                    await trigger.StartWatchingAsync(flowCts.Token);
                    registration.Triggers.Add(new ActiveTrigger(instance.Id, trigger, handler));
                    OnLog(flow.Id, instance.Id, $"Trigger activated: {node.Definition.DisplayName}");
                }
                catch (Exception ex)
                {
                    trigger.Triggered -= handler;
                    DisposeTrigger(trigger);

                    var message = $"Failed to activate trigger '{instance.Id}': {ex.Message}";
                    errors.Add(message);
                    OnError(flow.Id, instance.Id, message);
                }
            }
            catch (Exception ex)
            {
                var message = $"Failed to create trigger node '{instance.Id}': {ex.Message}";
                errors.Add(message);
                OnError(flow.Id, instance.Id, message);
            }
        }

        if (registration.Triggers.Count == 0)
        {
            await registration.DisposeAsync();
            return new FlowTriggerActivationResult
            {
                FlowId = flow.Id,
                FlowName = flow.Name,
                Armed = false,
                Errors = errors.ToArray()
            };
        }

        lock (_sync)
        {
            _activeFlows[flow.Id] = registration;
        }

        return new FlowTriggerActivationResult
        {
            FlowId = flow.Id,
            FlowName = flow.Name,
            Armed = true,
            ActiveTriggerNodeIds = registration.Triggers.Select(trigger => trigger.NodeId).ToArray(),
            Errors = errors.ToArray()
        };
    }

    public async Task DeactivateAllAsync()
    {
        List<ActiveFlowRegistration> registrations;
        lock (_sync)
        {
            registrations = _activeFlows.Values.ToList();
            _activeFlows.Clear();
        }

        foreach (var registration in registrations)
        {
            await registration.DisposeAsync();
            OnLog(registration.Flow.Id, registration.Flow.Id, "All triggers deactivated for flow.");
        }
    }

    public async Task DeactivateFlowAsync(string flowId)
    {
        ActiveFlowRegistration? registration = null;
        lock (_sync)
        {
            if (_activeFlows.TryGetValue(flowId, out registration))
            {
                _activeFlows.Remove(flowId);
            }
        }

        if (registration == null)
        {
            return;
        }

        await registration.DisposeAsync();
        OnLog(registration.Flow.Id, registration.Flow.Id, "Flow triggers deactivated.");
    }

    public string[] GetActiveTriggerNodeIds(string flowId)
    {
        lock (_sync)
        {
            return _activeFlows.TryGetValue(flowId, out var registration)
                ? registration.Triggers.Select(trigger => trigger.NodeId).ToArray()
                : [];
        }
    }

    private static bool IsArmableTriggerNode(NodeInstance instance)
    {
        return instance.TypeId.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(instance.TypeId, "trigger.manualStart", StringComparison.OrdinalIgnoreCase);
    }

    private void OnTriggerFired(Flow flow, string triggerNodeId, TriggerEventArgs args)
    {
        TriggerFired?.Invoke(this, new TriggerFiredEventArgs
        {
            Flow = flow,
            TriggerNodeId = triggerNodeId,
            Data = new Dictionary<string, object?>(args.Data, StringComparer.OrdinalIgnoreCase),
            Timestamp = args.Timestamp
        });

        OnLog(flow.Id, triggerNodeId, "Trigger fired.");
    }

    private void OnLog(string flowId, string nodeId, string message)
    {
        LogMessage?.Invoke(this, new TriggerManagerLogEventArgs
        {
            FlowId = flowId,
            NodeId = nodeId,
            Message = message
        });
    }

    private void OnError(string flowId, string nodeId, string error)
    {
        TriggerError?.Invoke(this, new TriggerManagerErrorEventArgs
        {
            FlowId = flowId,
            NodeId = nodeId,
            Error = error
        });
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        DeactivateAllAsync().GetAwaiter().GetResult();
        _disposeCts.Dispose();
    }

    private static void DisposeTrigger(ITriggerNode trigger)
    {
        if (trigger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed class ActiveFlowRegistration(Flow flow, CancellationTokenSource cts) : IAsyncDisposable
    {
        public Flow Flow { get; } = flow;
        public CancellationTokenSource Cancellation { get; } = cts;
        public List<ActiveTrigger> Triggers { get; } = [];

        public async ValueTask DisposeAsync()
        {
            Cancellation.Cancel();

            foreach (var trigger in Triggers)
            {
                try
                {
                    trigger.Trigger.Triggered -= trigger.Handler;
                    await trigger.Trigger.StopWatchingAsync();
                }
                catch
                {
                    // Trigger cleanup is best-effort during disarm and shutdown.
                }
                finally
                {
                    DisposeTrigger(trigger.Trigger);
                }
            }

            Triggers.Clear();
            Cancellation.Dispose();
        }
    }

    private sealed record ActiveTrigger(string NodeId, ITriggerNode Trigger, Action<TriggerEventArgs> Handler);
}
