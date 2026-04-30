using Ajudante.Core.Interfaces;
using System.Collections;
using System.Text.Json;

namespace Ajudante.Core.Engine;

public class FlowExecutor
{
    private readonly INodeRegistry _registry;
    private CancellationTokenSource? _cts;

    public int MaxStepsPerRun { get; set; } = 10_000;
    public int MaxRecursionDepth { get; set; } = 1_024;

    public event Action<string, NodeStatus>? NodeStatusChanged;
    public event Action<string, string>? LogMessage;
    public event Action<string>? FlowCompleted;
    public event Action<string, string>? FlowError;
    public event Action<string, string, string?, object?>? PhaseChanged;

    public bool IsRunning { get; private set; }

    public FlowExecutor(INodeRegistry registry)
    {
        _registry = registry;
    }

    public async Task ExecuteAsync(Flow flow, string? startNodeId = null, FlowExecutionContext? context = null)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        context ??= new FlowExecutionContext(flow, _cts.Token);

        await ExecuteInternalAsync(flow, startNodeId, context, null, _cts.Token, isTriggerRun: false);
    }

    public async Task ExecuteFromTriggerAsync(Flow flow, string triggerNodeId,
        Dictionary<string, object?> triggerData, CancellationToken externalCt)
    {
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        var context = new FlowExecutionContext(flow, _cts.Token);
        context.SetNodeOutputs(triggerNodeId, triggerData);

        await ExecuteInternalAsync(flow, triggerNodeId, context, "triggered", _cts.Token, isTriggerRun: true);
    }

    private async Task ExecuteInternalAsync(
        Flow flow,
        string? startNodeId,
        FlowExecutionContext context,
        string? startOutputPort,
        CancellationToken ct,
        bool isTriggerRun)
    {
        IsRunning = true;
        context.PhaseSink ??= (nodeId, phase, message, detail) =>
            PhaseChanged?.Invoke(nodeId, phase, message, detail);

        try
        {
            var nodes = CreateNodeInstances(flow);
            var graph = BuildAdjacencyMap(flow);

            var entryNodeId = startNodeId ?? FindEntryNode(flow);
            if (entryNodeId == null)
            {
                FlowError?.Invoke(flow.Id, "No entry node found in flow");
                return;
            }

            var state = new ExecutionState
            {
                MaxSteps = MaxStepsPerRun,
                MaxDepth = MaxRecursionDepth
            };

            if (isTriggerRun)
            {
                if (!nodes.TryGetValue(entryNodeId, out var triggerNode))
                {
                    FlowError?.Invoke(flow.Id, $"Trigger node '{entryNodeId}' was not found in the flow.");
                    return;
                }

                NodeStatusChanged?.Invoke(entryNodeId, NodeStatus.Running);
                LogMessage?.Invoke(entryNodeId, $"Triggered: {triggerNode.Definition.DisplayName}");
                NodeStatusChanged?.Invoke(entryNodeId, NodeStatus.Completed);

                await ExecuteConnectedNodesAsync(entryNodeId, startOutputPort, nodes, graph, flow, context, ct, state);
            }
            else
            {
                await ExecuteNodeChain(entryNodeId, nodes, graph, flow, context, ct, state);
            }

            if (state.AbortReason != null)
            {
                FlowError?.Invoke(flow.Id, state.AbortReason);
                return;
            }

            FlowCompleted?.Invoke(flow.Id);
        }
        catch (OperationCanceledException)
        {
            LogMessage?.Invoke(flow.Id, "Flow execution cancelled");
        }
        catch (Exception ex)
        {
            FlowError?.Invoke(flow.Id, ex.Message);
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ExecuteNodeChain(string nodeId, Dictionary<string, INode> nodes,
        Dictionary<string, List<ConnectionInfo>> graph, Flow flow,
        FlowExecutionContext context, CancellationToken ct)
    {
        var state = new ExecutionState
        {
            MaxSteps = MaxStepsPerRun,
            MaxDepth = MaxRecursionDepth
        };

        await ExecuteNodeChain(nodeId, nodes, graph, flow, context, ct, state);
    }

    private async Task ExecuteNodeChain(string nodeId, Dictionary<string, INode> nodes,
        Dictionary<string, List<ConnectionInfo>> graph, Flow flow,
        FlowExecutionContext context, CancellationToken ct, ExecutionState state)
    {
        if (state.AbortReason != null)
            return;

        ct.ThrowIfCancellationRequested();

        if (!nodes.TryGetValue(nodeId, out var node)) return;

        state.Steps++;
        if (state.Steps > state.MaxSteps)
        {
            state.AbortReason = $"Flow exceeded the maximum step budget ({state.MaxSteps}). This usually means a cycle or unbounded loop. Stopped at node {nodeId}.";
            LogMessage?.Invoke(nodeId, state.AbortReason);
            return;
        }

        state.Depth++;
        NodeStatusChanged?.Invoke(nodeId, NodeStatus.Running);
        LogMessage?.Invoke(nodeId, $"Executing: {node.Definition.DisplayName}");

        try
        {
            if (state.Depth > state.MaxDepth)
            {
                state.AbortReason = $"Flow exceeded the maximum recursion depth ({state.MaxDepth}). Stopped at node {nodeId}.";
                LogMessage?.Invoke(nodeId, state.AbortReason);
                return;
            }

            var previousNodeId = context.CurrentNodeId;
            context.CurrentNodeId = nodeId;
            NodeResult result;
            try
            {
                result = await node.ExecuteAsync(context, ct);
            }
            finally
            {
                context.CurrentNodeId = previousNodeId;
            }

            if (result.Success)
            {
                NodeStatusChanged?.Invoke(nodeId, NodeStatus.Completed);
                context.SetNodeOutputs(nodeId, result.Outputs);

                if (graph.TryGetValue(nodeId, out var connections))
                {
                    await ExecuteConnectedNodesAsync(nodeId, result.OutputPort, nodes, graph, flow, context, ct, state);
                }
            }
            else
            {
                NodeStatusChanged?.Invoke(nodeId, NodeStatus.Error);
                LogMessage?.Invoke(nodeId, $"Error: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            NodeStatusChanged?.Invoke(nodeId, NodeStatus.Skipped);
            throw;
        }
        catch (Exception ex)
        {
            NodeStatusChanged?.Invoke(nodeId, NodeStatus.Error);
            LogMessage?.Invoke(nodeId, $"Exception: {ex.Message}");
        }
        finally
        {
            state.Depth--;
        }
    }

    private async Task ExecuteConnectedNodesAsync(
        string sourceNodeId,
        string? outputPort,
        Dictionary<string, INode> nodes,
        Dictionary<string, List<ConnectionInfo>> graph,
        Flow flow,
        FlowExecutionContext context,
        CancellationToken ct,
        ExecutionState state)
    {
        if (!graph.TryGetValue(sourceNodeId, out var connections))
        {
            return;
        }

        var nextConnections = outputPort != null
            ? connections.Where(c => c.SourcePort == outputPort)
            : connections;

        foreach (var conn in nextConnections)
        {
            await ExecuteNodeChain(conn.TargetNodeId, nodes, graph, flow, context, ct, state);
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private Dictionary<string, INode> CreateNodeInstances(Flow flow)
    {
        var nodes = new Dictionary<string, INode>();
        foreach (var instance in flow.Nodes)
        {
            var node = _registry.CreateInstance(instance.TypeId);
            node.Id = instance.Id;
            node.Configure(NormalizeProperties(instance.Properties));
            nodes[instance.Id] = node;
        }
        return nodes;
    }

    private static Dictionary<string, object?> NormalizeProperties(Dictionary<string, object?> properties)
    {
        return properties.ToDictionary(
            pair => pair.Key,
            pair => NormalizeValue(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement element)
        {
            return NormalizeJsonElement(element);
        }

        if (value is Dictionary<string, object?> dictionary)
        {
            return NormalizeProperties(dictionary);
        }

        if (value is IEnumerable sequence && value is not string)
        {
            var items = new List<object?>();
            foreach (var item in sequence)
            {
                items.Add(NormalizeValue(item));
            }

            return items;
        }

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => NormalizeJsonElement(property.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, List<ConnectionInfo>> BuildAdjacencyMap(Flow flow)
    {
        var map = new Dictionary<string, List<ConnectionInfo>>();
        foreach (var conn in flow.Connections)
        {
            if (!map.ContainsKey(conn.SourceNodeId))
                map[conn.SourceNodeId] = new List<ConnectionInfo>();

            map[conn.SourceNodeId].Add(new ConnectionInfo
            {
                SourcePort = conn.SourcePort,
                TargetNodeId = conn.TargetNodeId,
                TargetPort = conn.TargetPort
            });
        }
        return map;
    }

    private static string? FindEntryNode(Flow flow)
    {
        var targetNodes = new HashSet<string>(flow.Connections.Select(c => c.TargetNodeId));
        return flow.Nodes.FirstOrDefault(n => !targetNodes.Contains(n.Id))?.Id;
    }

    private class ConnectionInfo
    {
        public required string SourcePort { get; init; }
        public required string TargetNodeId { get; init; }
        public required string TargetPort { get; init; }
    }

    private sealed class ExecutionState
    {
        public int Steps { get; set; }
        public int Depth { get; set; }
        public int MaxSteps { get; init; }
        public int MaxDepth { get; init; }
        public string? AbortReason { get; set; }
    }
}
