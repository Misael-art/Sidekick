using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public class FlowValidator
{
    private readonly INodeRegistry _registry;

    public FlowValidator(INodeRegistry registry)
    {
        _registry = registry;
    }

    public ValidationResult Validate(Flow flow)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (flow.Nodes.Count == 0)
            errors.Add("Flow has no nodes.");

        // Check all node types exist
        foreach (var node in flow.Nodes)
        {
            if (_registry.GetDefinition(node.TypeId) == null)
                errors.Add($"Node '{node.Id}' has unknown type '{node.TypeId}'.");
        }

        // Check connections reference valid nodes and ports
        var nodeIds = new HashSet<string>(flow.Nodes.Select(n => n.Id));
        foreach (var conn in flow.Connections)
        {
            if (!nodeIds.Contains(conn.SourceNodeId))
                errors.Add($"Connection '{conn.Id}' references missing source node '{conn.SourceNodeId}'.");
            if (!nodeIds.Contains(conn.TargetNodeId))
                errors.Add($"Connection '{conn.Id}' references missing target node '{conn.TargetNodeId}'.");
        }

        // Check for at least one trigger node
        var hasTrigger = flow.Nodes.Any(n =>
            _registry.GetDefinition(n.TypeId)?.Category == NodeCategory.Trigger);
        if (!hasTrigger)
            warnings.Add("Flow has no trigger node. It can only be started manually.");

        // Check for disconnected nodes
        var connectedNodes = new HashSet<string>();
        foreach (var conn in flow.Connections)
        {
            connectedNodes.Add(conn.SourceNodeId);
            connectedNodes.Add(conn.TargetNodeId);
        }
        foreach (var node in flow.Nodes)
        {
            if (!connectedNodes.Contains(node.Id) && flow.Nodes.Count > 1)
                warnings.Add($"Node '{node.Id}' ({node.TypeId}) is disconnected.");
        }

        // Check for cycles (simple DFS)
        if (HasCycle(flow))
            warnings.Add("Flow contains a cycle. Ensure loop nodes handle termination.");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static bool HasCycle(Flow flow)
    {
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var conn in flow.Connections)
        {
            if (!adjacency.ContainsKey(conn.SourceNodeId))
                adjacency[conn.SourceNodeId] = new List<string>();
            adjacency[conn.SourceNodeId].Add(conn.TargetNodeId);
        }

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        bool Dfs(string nodeId)
        {
            visited.Add(nodeId);
            stack.Add(nodeId);

            if (adjacency.TryGetValue(nodeId, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (stack.Contains(neighbor)) return true;
                    if (!visited.Contains(neighbor) && Dfs(neighbor)) return true;
                }
            }

            stack.Remove(nodeId);
            return false;
        }

        foreach (var nodeId in flow.Nodes.Select(n => n.Id))
        {
            if (!visited.Contains(nodeId) && Dfs(nodeId))
                return true;
        }

        return false;
    }
}

public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
