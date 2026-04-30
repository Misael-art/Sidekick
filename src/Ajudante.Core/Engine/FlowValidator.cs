using System.Text.Json;
using System.Text.RegularExpressions;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public class FlowValidator
{
    private static readonly Regex TemplateReferenceRegex = new(@"\{\{([\w\.\-:]+)\}\}", RegexOptions.Compiled);
    private static readonly HashSet<string> BuiltInVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "loopIndex",
        "loopIteration",
        "loopCount"
    };
    private static readonly HashSet<string> RequiredPropertyIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "filePath",
        "folderPath",
        "sourcePath",
        "destinationPath",
        "path",
        "url",
        "variableName",
        "templateImage"
    };
    private readonly INodeRegistry _registry;

    public FlowValidator(INodeRegistry registry)
    {
        _registry = registry;
    }

    public ValidationResult Validate(Flow flow)
    {
        var issues = new List<ValidationIssue>();
        var definitionsByNodeId = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
        var nodesById = new Dictionary<string, NodeInstance>(StringComparer.OrdinalIgnoreCase);
        var knownVariables = CollectKnownVariables(flow);

        if (flow.Nodes.Count == 0)
            issues.Add(ValidationIssue.Error("flow.empty", "Flow has no nodes."));

        foreach (var node in flow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                issues.Add(ValidationIssue.Error("node.id.missing", "A node is missing its id."));
                continue;
            }

            if (!nodesById.TryAdd(node.Id, node))
            {
                issues.Add(ValidationIssue.Error("node.id.duplicate", $"Flow contains duplicate node id '{node.Id}'.", nodeId: node.Id));
                continue;
            }

            var definition = _registry.GetDefinition(node.TypeId);
            if (definition == null)
            {
                issues.Add(ValidationIssue.Error("node.type.unknown", $"Node '{node.Id}' has unknown type '{node.TypeId}'.", nodeId: node.Id));
                continue;
            }

            definitionsByNodeId[node.Id] = definition;
        }

        foreach (var node in flow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !definitionsByNodeId.TryGetValue(node.Id, out var definition))
                continue;

            ValidateNodeProperties(node, definition, nodesById, definitionsByNodeId, knownVariables, issues);
        }

        foreach (var conn in flow.Connections)
        {
            ValidateConnection(conn, nodesById, definitionsByNodeId, issues);
        }

        var hasTrigger = flow.Nodes.Any(n =>
            _registry.GetDefinition(n.TypeId)?.Category == NodeCategory.Trigger);
        if (!hasTrigger)
            issues.Add(ValidationIssue.Warning("flow.trigger.missing", "Flow has no trigger node. It can only be started manually."));

        var connectedNodes = new HashSet<string>();
        foreach (var conn in flow.Connections)
        {
            connectedNodes.Add(conn.SourceNodeId);
            connectedNodes.Add(conn.TargetNodeId);
        }
        foreach (var node in flow.Nodes)
        {
            if (!connectedNodes.Contains(node.Id) && flow.Nodes.Count > 1)
                issues.Add(ValidationIssue.Warning("node.disconnected", $"Node '{node.Id}' ({node.TypeId}) is disconnected.", nodeId: node.Id));
        }

        if (HasCycle(flow))
            issues.Add(ValidationIssue.Warning("flow.cycle", "Flow contains a cycle. Ensure loop nodes handle termination."));

        var errors = issues
            .Where(issue => issue.Severity == ValidationSeverity.Error)
            .Select(issue => issue.Message)
            .Distinct()
            .ToList();
        var warnings = issues
            .Where(issue => issue.Severity == ValidationSeverity.Warning)
            .Select(issue => issue.Message)
            .Distinct()
            .ToList();

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Issues = issues
        };
    }

    private static HashSet<string> CollectKnownVariables(Flow flow)
    {
        var knownVariables = new HashSet<string>(BuiltInVariables, StringComparer.OrdinalIgnoreCase);

        foreach (var variable in flow.Variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Name))
                knownVariables.Add(variable.Name);
        }

        foreach (var node in flow.Nodes)
        {
            if (node.Properties.TryGetValue("storeInVariable", out var storeInVariable) &&
                TryGetStringValue(storeInVariable, out var storeVariableName) &&
                !string.IsNullOrWhiteSpace(storeVariableName))
            {
                knownVariables.Add(storeVariableName);
            }

            if (string.Equals(node.TypeId, "logic.setVariable", StringComparison.OrdinalIgnoreCase) &&
                node.Properties.TryGetValue("variableName", out var variableName) &&
                TryGetStringValue(variableName, out var explicitVariableName) &&
                !string.IsNullOrWhiteSpace(explicitVariableName) &&
                !ContainsTemplate(explicitVariableName))
            {
                knownVariables.Add(explicitVariableName);
            }

            if (node.Properties.TryGetValue("counterVariable", out var counterVariable) &&
                TryGetStringValue(counterVariable, out var counterVariableName) &&
                !string.IsNullOrWhiteSpace(counterVariableName) &&
                !ContainsTemplate(counterVariableName))
            {
                knownVariables.Add(counterVariableName);
            }
        }

        return knownVariables;
    }

    private static void ValidateNodeProperties(
        NodeInstance node,
        NodeDefinition definition,
        IReadOnlyDictionary<string, NodeInstance> nodesById,
        IReadOnlyDictionary<string, NodeDefinition> definitionsByNodeId,
        IReadOnlySet<string> knownVariables,
        ICollection<ValidationIssue> issues)
    {
        foreach (var property in definition.Properties)
        {
            node.Properties.TryGetValue(property.Id, out var rawValue);

            if (IsRequired(property) && IsBlankValue(rawValue))
            {
                issues.Add(ValidationIssue.Error(
                    "property.required",
                    $"Node '{node.Id}' is missing required property '{property.Name}'.",
                    nodeId: node.Id,
                    propertyId: property.Id));
                continue;
            }

            if (string.Equals(property.Id, "timeoutMs", StringComparison.OrdinalIgnoreCase) &&
                TryGetIntValue(rawValue, out var timeoutMs) &&
                timeoutMs <= 0)
            {
                issues.Add(ValidationIssue.Error(
                    "property.timeout.invalid",
                    $"Node '{node.Id}' has invalid timeout '{timeoutMs}' in property '{property.Name}'.",
                    nodeId: node.Id,
                    propertyId: property.Id));
            }

            if (property.Type is PropertyType.FilePath or PropertyType.FolderPath &&
                TryGetStringValue(rawValue, out var pathValue) &&
                !string.IsNullOrWhiteSpace(pathValue) &&
                !ContainsTemplate(pathValue) &&
                pathValue.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                issues.Add(ValidationIssue.Error(
                    "property.path.invalid",
                    $"Node '{node.Id}' has invalid path value in property '{property.Name}'.",
                    nodeId: node.Id,
                    propertyId: property.Id));
            }

            if (property.Type == PropertyType.ImageTemplate &&
                !IsValidImageTemplateValue(rawValue))
            {
                issues.Add(ValidationIssue.Error(
                    "property.imageTemplate.invalid",
                    $"Node '{node.Id}' has no valid image template configured in property '{property.Name}'.",
                    nodeId: node.Id,
                    propertyId: property.Id));
            }

            foreach (var reference in ExtractTemplateReferences(rawValue))
            {
                if (!IsKnownReference(reference, nodesById, definitionsByNodeId, knownVariables, out var referenceError))
                {
                    issues.Add(ValidationIssue.Error(
                        "property.template.unresolved",
                        $"Node '{node.Id}' has unresolved reference '{reference}' in property '{property.Name}': {referenceError}",
                        nodeId: node.Id,
                        propertyId: property.Id));
                }
            }
        }

        ValidateSelector(node, definition, issues);
    }

    private static void ValidateSelector(NodeInstance node, NodeDefinition definition, ICollection<ValidationIssue> issues)
    {
        var hasSelectorProperties = definition.Properties.Any(property =>
            string.Equals(property.Id, "windowTitle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "automationId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "elementName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "controlType", StringComparison.OrdinalIgnoreCase));

        if (!hasSelectorProperties)
            return;

        var windowTitle = GetStringValue(node.Properties, "windowTitle");
        var automationId = GetStringValue(node.Properties, "automationId");
        var elementName = GetStringValue(node.Properties, "elementName");
        var controlType = GetStringValue(node.Properties, "controlType");

        if (string.IsNullOrWhiteSpace(automationId) &&
            string.IsNullOrWhiteSpace(elementName) &&
            string.IsNullOrWhiteSpace(controlType))
        {
            issues.Add(ValidationIssue.Error(
                "selector.incomplete",
                $"Node '{node.Id}' has an incomplete selector. Configure Automation ID, Element Name, or Control Type.",
                nodeId: node.Id));
        }

        if (string.IsNullOrWhiteSpace(windowTitle) &&
            !string.IsNullOrWhiteSpace(automationId) &&
            string.IsNullOrWhiteSpace(elementName))
        {
            issues.Add(ValidationIssue.Warning(
                "selector.ambiguous",
                $"Node '{node.Id}' may be ambiguous without Window Title or Element Name in the selector.",
                nodeId: node.Id));
        }
    }

    private static void ValidateConnection(
        Connection conn,
        IReadOnlyDictionary<string, NodeInstance> nodesById,
        IReadOnlyDictionary<string, NodeDefinition> definitionsByNodeId,
        ICollection<ValidationIssue> issues)
    {
        if (!nodesById.ContainsKey(conn.SourceNodeId))
        {
            issues.Add(ValidationIssue.Error(
                "connection.source.missing",
                $"Connection '{conn.Id}' references missing source node '{conn.SourceNodeId}'.",
                connectionId: conn.Id));
        }

        if (!nodesById.ContainsKey(conn.TargetNodeId))
        {
            issues.Add(ValidationIssue.Error(
                "connection.target.missing",
                $"Connection '{conn.Id}' references missing target node '{conn.TargetNodeId}'.",
                connectionId: conn.Id));
        }

        if (definitionsByNodeId.TryGetValue(conn.SourceNodeId, out var sourceDefinition) &&
            sourceDefinition.OutputPorts.All(port => !string.Equals(port.Id, conn.SourcePort, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(ValidationIssue.Error(
                "connection.sourcePort.invalid",
                $"Connection '{conn.Id}' references invalid source port '{conn.SourcePort}' on node '{conn.SourceNodeId}'.",
                nodeId: conn.SourceNodeId,
                connectionId: conn.Id));
        }

        if (definitionsByNodeId.TryGetValue(conn.TargetNodeId, out var targetDefinition) &&
            targetDefinition.InputPorts.All(port => !string.Equals(port.Id, conn.TargetPort, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(ValidationIssue.Error(
                "connection.targetPort.invalid",
                $"Connection '{conn.Id}' references invalid target port '{conn.TargetPort}' on node '{conn.TargetNodeId}'.",
                nodeId: conn.TargetNodeId,
                connectionId: conn.Id));
        }
    }

    private static bool IsRequired(PropertyDefinition property)
    {
        return RequiredPropertyIds.Contains(property.Id);
    }

    private static bool IsBlankValue(object? value)
    {
        if (value is null)
            return true;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text);

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => true,
                JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                JsonValueKind.Object => !element.EnumerateObject().Any(),
                _ => false
            };
        }

        return false;
    }

    private static bool IsValidImageTemplateValue(object? value)
    {
        if (value is null)
            return false;

        if (value is string text)
            return !string.IsNullOrWhiteSpace(text);

        if (value is byte[] bytes)
            return bytes.Length > 0;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return !string.IsNullOrWhiteSpace(element.GetString());

            if (element.ValueKind != JsonValueKind.Object)
                return false;

            var assetId = TryGetJsonString(element, "assetId");
            var imageBase64 = TryGetJsonString(element, "imageBase64");
            return !string.IsNullOrWhiteSpace(assetId) || !string.IsNullOrWhiteSpace(imageBase64);
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue("assetId", out var assetId) && !IsBlankValue(assetId) ||
                   dictionary.TryGetValue("imageBase64", out var imageBase64) && !IsBlankValue(imageBase64);
        }

        if (value is IDictionary<string, object?> mutableDictionary)
        {
            return mutableDictionary.TryGetValue("assetId", out var assetId) && !IsBlankValue(assetId) ||
                   mutableDictionary.TryGetValue("imageBase64", out var imageBase64) && !IsBlankValue(imageBase64);
        }

        return false;
    }

    private static IEnumerable<string> ExtractTemplateReferences(object? value)
    {
        if (value is string text)
        {
            foreach (Match match in TemplateReferenceRegex.Matches(text))
                yield return match.Groups[1].Value;

            yield break;
        }

        if (value is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    foreach (var reference in ExtractTemplateReferences(element.GetString()))
                        yield return reference;
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        foreach (var reference in ExtractTemplateReferences(property.Value))
                            yield return reference;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        foreach (var reference in ExtractTemplateReferences(item))
                            yield return reference;
                    }
                    break;
            }
        }
    }

    private static bool IsKnownReference(
        string reference,
        IReadOnlyDictionary<string, NodeInstance> nodesById,
        IReadOnlyDictionary<string, NodeDefinition> definitionsByNodeId,
        IReadOnlySet<string> knownVariables,
        out string error)
    {
        if (reference.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
        {
            var variableName = reference[4..];
            if (knownVariables.Contains(variableName))
            {
                error = string.Empty;
                return true;
            }

            error = $"variable '{variableName}' is not declared or produced by any node";
            return false;
        }

        var dotIndex = reference.IndexOf('.');
        if (dotIndex > 0 && dotIndex < reference.Length - 1)
        {
            var nodeId = reference[..dotIndex];
            var outputId = reference[(dotIndex + 1)..];

            if (!nodesById.ContainsKey(nodeId))
            {
                error = $"node '{nodeId}' does not exist";
                return false;
            }

            if (!definitionsByNodeId.TryGetValue(nodeId, out var nodeDefinition))
            {
                error = $"node '{nodeId}' has no known definition";
                return false;
            }

            if (nodeDefinition.OutputPorts.All(port => !string.Equals(port.Id, outputId, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"output '{outputId}' does not exist on node '{nodeId}'";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (knownVariables.Contains(reference))
        {
            error = string.Empty;
            return true;
        }

        error = $"variable '{reference}' is not declared or produced by any node";
        return false;
    }

    private static bool ContainsTemplate(string value)
    {
        return value.Contains("{{", StringComparison.Ordinal);
    }

    private static string GetStringValue(IReadOnlyDictionary<string, object?> properties, string key)
    {
        return properties.TryGetValue(key, out var value) && TryGetStringValue(value, out var text)
            ? text
            : string.Empty;
    }

    private static bool TryGetStringValue(object? value, out string text)
    {
        switch (value)
        {
            case string stringValue:
                text = stringValue;
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String:
                text = jsonElement.GetString() ?? string.Empty;
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    private static bool TryGetIntValue(object? value, out int number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                number = (int)longValue;
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var jsonInt):
                number = jsonInt;
                return true;
            case string text when int.TryParse(text, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static string? TryGetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            return property.GetString();

        return null;
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
    public List<ValidationIssue> Issues { get; init; } = new();
}

public class ValidationIssue
{
    public required ValidationSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? NodeId { get; init; }
    public string? ConnectionId { get; init; }
    public string? PropertyId { get; init; }

    public static ValidationIssue Error(string code, string message, string? nodeId = null, string? connectionId = null, string? propertyId = null)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Code = code,
            Message = message,
            NodeId = nodeId,
            ConnectionId = connectionId,
            PropertyId = propertyId
        };
    }

    public static ValidationIssue Warning(string code, string message, string? nodeId = null, string? connectionId = null, string? propertyId = null)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Code = code,
            Message = message,
            NodeId = nodeId,
            ConnectionId = connectionId,
            PropertyId = propertyId
        };
    }
}

public enum ValidationSeverity
{
    Error,
    Warning
}
