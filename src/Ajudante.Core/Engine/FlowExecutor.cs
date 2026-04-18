using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public class FlowExecutor
{
    private readonly INodeRegistry _registry;
    private CancellationTokenSource? _cts;

    public event Action<string, NodeStatus>? NodeStatusChanged;
    public event Action<string, string>? LogMessage;
    public event Action<string>? FlowCompleted;
    public event Action<string, string>? FlowError;

    public bool IsRunning { get; private set; }

    public FlowExecutor(INodeRegistry registry)
    {
        _registry = registry;
    }

    public async Task ExecuteAsync(Flow flow, string? startNodeId = null, FlowExecutionContext? context = null)
    {
        _cts = new CancellationTokenSource();
        context ??= new FlowExecutionContext(flow, _cts.Token);
        IsRunning = true;

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

            await ExecuteNodeChain(entryNodeId, nodes, graph, flow, context, _cts.Token);
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
        }
    }

    public async Task ExecuteFromTriggerAsync(Flow flow, string triggerNodeId,
        Dictionary<string, object?> triggerData, CancellationToken externalCt)
    {
        var context = new FlowExecutionContext(flow, externalCt);
        context.SetNodeOutputs(triggerNodeId, triggerData);

        var nodes = CreateNodeInstances(flow);
        var graph = BuildAdjacencyMap(flow);

        await ExecuteNodeChain(triggerNodeId, nodes, graph, flow, context, externalCt);
    }

    private async Task ExecuteNodeChain(string nodeId, Dictionary<string, INode> nodes,
        Dictionary<string, List<ConnectionInfo>> graph, Flow flow,
        FlowExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!nodes.TryGetValue(nodeId, out var node)) return;

        NodeStatusChanged?.Invoke(nodeId, NodeStatus.Running);
        LogMessage?.Invoke(nodeId, $"Executing: {node.Definition.DisplayName}");

        try
        {
            var result = await node.ExecuteAsync(context, ct);

            if (result.Success)
            {
                NodeStatusChanged?.Invoke(nodeId, NodeStatus.Completed);
                context.SetNodeOutputs(nodeId, result.Outputs);

                if (graph.TryGetValue(nodeId, out var connections))
                {
                    var nextConnections = result.OutputPort != null
                        ? connections.Where(c => c.SourcePort == result.OutputPort)
                        : connections;

                    foreach (var conn in nextConnections)
                    {
                        await ExecuteNodeChain(conn.TargetNodeId, nodes, graph, flow, context, ct);
                    }
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
            node.Configure(instance.Properties);
            nodes[instance.Id] = node;
        }
        return nodes;
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
}
