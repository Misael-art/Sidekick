using System.Collections.Concurrent;
using System.Reflection;
using Ajudante.App.Runtime;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Tests;

public class FlowRuntimeManagerTests
{
    [Fact]
    public async Task ActivateAndDeactivateFlow_KeepsArmedFlowsSeparatedFromRunningState()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        using var runtime = new FlowRuntimeManager(registry);

        var flowA = CreateTriggerFlow("flow-a", "Flow A");
        var flowB = CreateTriggerFlow("flow-b", "Flow B");

        await runtime.ActivateFlowAsync(flowA);
        await runtime.ActivateFlowAsync(flowB);

        var activeFlows = runtime.ListActiveFlows();
        Assert.Equal(2, activeFlows.Length);
        Assert.All(activeFlows, snapshot => Assert.True(snapshot.IsArmed));

        var status = runtime.GetRuntimeStatus();
        Assert.False(status.IsRunning);
        Assert.Equal(2, status.ArmedFlowCount);

        var deactivated = await runtime.DeactivateFlowAsync(flowA.Id);
        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsArmed);

        status = runtime.GetRuntimeStatus();
        Assert.Equal(1, status.ArmedFlowCount);
        Assert.Single(runtime.ListActiveFlows());
        Assert.Equal("flow-b", runtime.ListActiveFlows()[0].FlowId);
    }

    [Fact]
    public async Task TriggerFiring_QueuesExecutionAndUpdatesRuntimeSnapshot()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        using var runtime = new FlowRuntimeManager(registry);
        var flow = CreateTriggerFlow("flow-trigger", "Trigger Flow");

        await runtime.ActivateFlowAsync(flow);
        var trigger = registry.CreatedTriggers.Single();

        trigger.Fire(new Dictionary<string, object?> { ["value"] = 42 });

        await WaitUntilAsync(() =>
        {
            var snapshot = FindFlowSnapshot(runtime, flow.Id);
            return snapshot?.LastTriggerAt != null && snapshot.LastRunAt != null && !snapshot.IsRunning;
        });

        var finalSnapshot = FindFlowSnapshot(runtime, flow.Id);
        Assert.NotNull(finalSnapshot);
        Assert.True(finalSnapshot!.IsArmed);
        Assert.NotNull(finalSnapshot.LastTriggerAt);
        Assert.NotNull(finalSnapshot.LastRunAt);
        Assert.False(runtime.GetRuntimeStatus().IsRunning);
    }

    [Fact]
    public async Task TriggerBurst_CoalescesToSinglePendingRunPerFlow()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        using var runtime = new FlowRuntimeManager(registry);
        var flow = CreateTriggerFlow("flow-coalesce", "Coalesced Flow", delayMs: 150);

        var completedRuns = 0;
        var coalescedEvents = 0;
        runtime.FlowCompleted += _ => Interlocked.Increment(ref completedRuns);
        runtime.FlowQueueCoalesced += (_, _) => Interlocked.Increment(ref coalescedEvents);

        await runtime.ActivateFlowAsync(flow);
        var trigger = registry.CreatedTriggers.Single();

        trigger.Fire();
        await WaitUntilAsync(() => runtime.GetRuntimeStatus().IsRunning);

        trigger.Fire();
        trigger.Fire();

        Assert.True(runtime.GetRuntimeStatus().QueueLength <= 1);

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return !status.IsRunning && status.QueueLength == 0 && completedRuns == 2;
        });

        Assert.True(coalescedEvents >= 1);
    }

    [Fact]
    public async Task StopCurrentOnly_CancelsActiveRunButLeavesPendingRunQueued()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        using var runtime = new FlowRuntimeManager(registry);
        var flow = CreateTriggerFlow("flow-stop-current", "Stop Current", delayMs: 250);

        var completedRuns = 0;
        runtime.FlowCompleted += _ => Interlocked.Increment(ref completedRuns);

        runtime.QueueManualRun(flow);
        await WaitUntilAsync(() => runtime.GetRuntimeStatus().IsRunning);
        runtime.QueueManualRun(flow);

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return status.IsRunning && status.QueueLength == 1;
        });

        var stopResult = runtime.Stop(StopFlowMode.CurrentOnly);
        Assert.True(stopResult.CancelledCurrentRun);
        Assert.Equal(0, stopResult.ClearedQueuedRuns);

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return !status.IsRunning && status.QueueLength == 0 && completedRuns == 1;
        });
    }

    [Fact]
    public async Task StopCancelAll_CancelsActiveRunAndClearsPendingQueue()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        using var runtime = new FlowRuntimeManager(registry);
        var flow = CreateTriggerFlow("flow-stop-all", "Stop All", delayMs: 1000);

        var completedRuns = 0;
        runtime.FlowCompleted += _ => Interlocked.Increment(ref completedRuns);

        runtime.QueueManualRun(flow);
        await WaitUntilAsync(() => runtime.GetRuntimeStatus().IsRunning);
        runtime.QueueManualRun(flow);

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return status.IsRunning && status.QueueLength == 1;
        });

        var stopResult = runtime.Stop(StopFlowMode.CancelAll);
        Assert.True(stopResult.CancelledCurrentRun);
        Assert.Equal(1, stopResult.ClearedQueuedRuns);

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return !status.IsRunning && status.QueueLength == 0;
        });

        Assert.Equal(0, completedRuns);
    }

    [Fact]
    public async Task Dispose_CleansUpArmedTriggersAndQueuedWork()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());

        var runtime = new FlowRuntimeManager(registry);
        var flow = CreateTriggerFlow("flow-dispose", "Dispose Flow", delayMs: 250);

        await runtime.ActivateFlowAsync(flow);
        var trigger = registry.CreatedTriggers.Single();
        trigger.Fire();
        trigger.Fire();

        await WaitUntilAsync(() =>
        {
            var status = runtime.GetRuntimeStatus();
            return status.IsRunning || status.QueueLength > 0;
        });

        runtime.Dispose();

        Assert.Equal(1, trigger.StartCount);
        Assert.True(trigger.StopCount >= 1);
        Assert.True(trigger.Disposed);
    }

    [Fact]
    public async Task QueueManualRun_PersistsCompletedExecutionHistory()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());
        var historyStore = new InMemoryExecutionHistoryStore();

        using var runtime = new FlowRuntimeManager(registry, historyStore);
        var flow = CreateTriggerFlow("flow-history-complete", "History Complete");

        runtime.QueueManualRun(flow);

        await WaitUntilAsync(() => historyStore.Entries.Any(entry =>
            string.Equals(entry.FlowId, flow.Id, StringComparison.OrdinalIgnoreCase) &&
            entry.Result == FlowExecutionResult.Completed));

        var history = await runtime.GetExecutionHistoryAsync();
        var entry = Assert.Single(history);
        Assert.Equal(flow.Id, entry.FlowId);
        Assert.Equal(FlowExecutionResult.Completed, entry.Result);
        Assert.NotNull(entry.FinishedAt);
        Assert.NotEmpty(entry.NodeStatuses);
    }

    [Fact]
    public async Task StopCurrentOnly_PersistsCancelledExecutionHistory()
    {
        var registry = new TestNodeRegistry();
        registry.Register("trigger.test", CreateTriggerDefinition(), () => new TestTriggerNode());
        registry.Register("action.testDelay", CreateActionDefinition(), () => new TestActionNode());
        var historyStore = new InMemoryExecutionHistoryStore();

        using var runtime = new FlowRuntimeManager(registry, historyStore);
        var flow = CreateTriggerFlow("flow-history-cancelled", "History Cancelled", delayMs: 500);

        runtime.QueueManualRun(flow);
        await WaitUntilAsync(() => runtime.GetRuntimeStatus().IsRunning);

        var stopResult = runtime.Stop(StopFlowMode.CurrentOnly);
        Assert.True(stopResult.CancelledCurrentRun);

        await WaitUntilAsync(() => historyStore.Entries.Any(entry =>
            string.Equals(entry.FlowId, flow.Id, StringComparison.OrdinalIgnoreCase) &&
            entry.Result == FlowExecutionResult.Cancelled));

        var history = await runtime.GetExecutionHistoryAsync();
        var entry = Assert.Single(history);
        Assert.Equal(FlowExecutionResult.Cancelled, entry.Result);
        Assert.NotNull(entry.FinishedAt);
        Assert.Contains(entry.Logs, log => log.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
    }

    private static FlowRuntimeSnapshot? FindFlowSnapshot(FlowRuntimeManager runtime, string flowId)
    {
        return runtime.GetRuntimeStatus().Flows.FirstOrDefault(flow => string.Equals(flow.FlowId, flowId, StringComparison.OrdinalIgnoreCase));
    }

    private static NodeDefinition CreateTriggerDefinition()
    {
        return new NodeDefinition
        {
            TypeId = "trigger.test",
            DisplayName = "Test Trigger",
            Category = NodeCategory.Trigger,
            InputPorts = [],
            OutputPorts =
            [
                new PortDefinition { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow }
            ]
        };
    }

    private static NodeDefinition CreateActionDefinition()
    {
        return new NodeDefinition
        {
            TypeId = "action.testDelay",
            DisplayName = "Delay Action",
            Category = NodeCategory.Action,
            InputPorts =
            [
                new PortDefinition { Id = "in", Name = "In", DataType = PortDataType.Flow }
            ],
            OutputPorts =
            [
                new PortDefinition { Id = "out", Name = "Out", DataType = PortDataType.Flow }
            ],
            Properties =
            [
                new PropertyDefinition
                {
                    Id = "delayMs",
                    Name = "Delay",
                    Type = PropertyType.Integer,
                    DefaultValue = 0
                }
            ]
        };
    }

    private static Flow CreateTriggerFlow(string flowId, string flowName, int delayMs = 0)
    {
        return new Flow
        {
            Id = flowId,
            Name = flowName,
            Nodes =
            [
                new NodeInstance
                {
                    Id = "trigger",
                    TypeId = "trigger.test"
                },
                new NodeInstance
                {
                    Id = "action",
                    TypeId = "action.testDelay",
                    Properties = new Dictionary<string, object?> { ["delayMs"] = delayMs }
                }
            ],
            Connections =
            [
                new Connection
                {
                    Id = "c1",
                    SourceNodeId = "trigger",
                    SourcePort = "triggered",
                    TargetNodeId = "action",
                    TargetPort = "in"
                }
            ]
        };
    }

    private sealed class TestNodeRegistry : INodeRegistry
    {
        private readonly Dictionary<string, NodeDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Func<INode>> _factories = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentBag<TestTriggerNode> CreatedTriggers { get; } = [];

        public void Register(string typeId, NodeDefinition definition, Func<INode> factory)
        {
            _definitions[typeId] = definition;
            _factories[typeId] = () =>
            {
                var node = factory();
                if (node is TestTriggerNode triggerNode)
                {
                    CreatedTriggers.Add(triggerNode);
                }

                return node;
            };
        }

        public void ScanAssembly(Assembly assembly) { }

        public void ScanDirectory(string pluginPath) { }

        public NodeDefinition[] GetAllDefinitions() => _definitions.Values.ToArray();

        public INode CreateInstance(string typeId)
        {
            if (!_factories.TryGetValue(typeId, out var factory))
            {
                throw new InvalidOperationException($"Unknown node type: {typeId}");
            }

            return factory();
        }

        public NodeDefinition? GetDefinition(string typeId)
            => _definitions.TryGetValue(typeId, out var definition) ? definition : null;
    }

    private sealed class TestTriggerNode : ITriggerNode, IDisposable
    {
        public string Id { get; set; } = "";
        public event Action<TriggerEventArgs>? Triggered;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public bool Disposed { get; private set; }
        public bool IsWatching { get; private set; }

        public NodeDefinition Definition => CreateTriggerDefinition();

        public void Configure(Dictionary<string, object?> properties)
        {
        }

        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(NodeResult.Ok("triggered"));
        }

        public Task StartWatchingAsync(CancellationToken ct)
        {
            StartCount++;
            IsWatching = true;
            return Task.CompletedTask;
        }

        public Task StopWatchingAsync()
        {
            StopCount++;
            IsWatching = false;
            return Task.CompletedTask;
        }

        public void Fire(Dictionary<string, object?>? data = null)
        {
            Assert.True(IsWatching, "Trigger must be armed before firing.");

            Triggered?.Invoke(new TriggerEventArgs
            {
                Data = data ?? new Dictionary<string, object?>(),
                Timestamp = DateTime.UtcNow
            });
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class TestActionNode : IActionNode
    {
        private int _delayMs;

        public string Id { get; set; } = "";

        public NodeDefinition Definition => CreateActionDefinition();

        public void Configure(Dictionary<string, object?> properties)
        {
            if (properties.TryGetValue("delayMs", out var delayMs) && delayMs != null)
            {
                _delayMs = Convert.ToInt32(delayMs);
            }
        }

        public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, ct);
            }

            return NodeResult.Ok("out");
        }
    }

    private sealed class InMemoryExecutionHistoryStore : IExecutionHistoryStore
    {
        private readonly object _sync = new();
        private readonly List<FlowExecutionHistoryEntry> _entries = [];

        public IReadOnlyList<FlowExecutionHistoryEntry> Entries
        {
            get
            {
                lock (_sync)
                {
                    return _entries.ToArray();
                }
            }
        }

        public Task<FlowExecutionHistoryEntry[]> ListAsync(int limit = 50, string? flowId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                IEnumerable<FlowExecutionHistoryEntry> query = _entries;
                if (!string.IsNullOrWhiteSpace(flowId))
                {
                    query = query.Where(entry => string.Equals(entry.FlowId, flowId, StringComparison.OrdinalIgnoreCase));
                }

                return Task.FromResult(
                    query.OrderByDescending(entry => entry.StartedAt)
                        .Take(limit)
                        .ToArray());
            }
        }

        public Task UpsertAsync(FlowExecutionHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                var index = _entries.FindIndex(existing =>
                    string.Equals(existing.RunId, entry.RunId, StringComparison.OrdinalIgnoreCase));

                if (index >= 0)
                {
                    _entries[index] = entry;
                }
                else
                {
                    _entries.Add(entry);
                }
            }

            return Task.CompletedTask;
        }
    }
}
