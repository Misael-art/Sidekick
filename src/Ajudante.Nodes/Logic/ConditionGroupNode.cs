using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.conditionGroup",
    DisplayName = "Condition Group",
    Category = NodeCategory.Logic,
    Description = "Evaluates nested ANY/ALL conditions with text and numeric operators",
    Color = "#EAB308")]
public sealed class ConditionGroupNode : ILogicNode
{
    private static readonly ConcurrentDictionary<string, string?> PreviousValues = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.conditionGroup",
        DisplayName = "Condition Group",
        Category = NodeCategory.Logic,
        Description = "Evaluates nested ANY/ALL conditions with text and numeric operators",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "true", Name = "True", DataType = PortDataType.Flow },
            new() { Id = "false", Name = "False", DataType = PortDataType.Flow },
            new() { Id = "result", Name = "Result", DataType = PortDataType.Boolean }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "ALL", Description = "Top-level group mode", Options = new[] { "ALL", "ANY" } },
            new() { Id = "conditionsJson", Name = "Conditions JSON", Type = PropertyType.String, DefaultValue = "[]", Description = "JSON array with conditions and nested groups" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to store boolean result" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) =>
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var mode = NodeValueHelper.GetString(_properties, "mode", "ALL");
            var conditionsJson = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "conditionsJson", "[]"));
            var variableName = NodeValueHelper.GetString(_properties, "storeInVariable");

            using var document = JsonDocument.Parse(conditionsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Task.FromResult(NodeResult.Fail("ConditionGroup expects conditionsJson as a JSON array."));

            var result = EvaluateGroup(document.RootElement, mode, context);
            NodeValueHelper.SetVariableIfRequested(context, variableName, result);

            return Task.FromResult(NodeResult.Ok(result ? "true" : "false", new Dictionary<string, object?>
            {
                ["result"] = result
            }));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(NodeResult.Fail($"ConditionGroup JSON parse error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"ConditionGroup execution failed: {ex.Message}"));
        }
    }

    private static bool EvaluateGroup(JsonElement elements, string mode, FlowExecutionContext context)
    {
        var isAny = string.Equals(mode, "ANY", StringComparison.OrdinalIgnoreCase);
        var hasEntries = false;
        var accumulator = isAny ? false : true;

        foreach (var element in elements.EnumerateArray())
        {
            hasEntries = true;
            var evaluation = EvaluateElement(element, context);

            if (isAny)
            {
                accumulator |= evaluation;
                if (accumulator)
                    return true;
            }
            else
            {
                accumulator &= evaluation;
                if (!accumulator)
                    return false;
            }
        }

        if (!hasEntries)
            return false;

        return accumulator;
    }

    private static bool EvaluateElement(JsonElement element, FlowExecutionContext context)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (element.TryGetProperty("conditions", out var nestedConditions) && nestedConditions.ValueKind == JsonValueKind.Array)
        {
            var nestedMode = GetString(element, "mode", "ALL");
            return EvaluateGroup(nestedConditions, nestedMode, context);
        }

        var left = context.ResolveTemplate(GetString(element, "left", ""));
        var op = GetString(element, "operator", "equals").ToLowerInvariant();
        var right = context.ResolveTemplate(GetString(element, "right", ""));
        var key = context.ResolveTemplate(GetString(element, "key", left));

        return op switch
        {
            "equals" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "regex" => SafeRegexMatch(left, right),
            "greater" => CompareNumeric(left, right, comparison: "greater"),
            "less" => CompareNumeric(left, right, comparison: "less"),
            "exists" => !string.IsNullOrWhiteSpace(left),
            "changed" => EvaluateChanged(key, left),
            _ => false
        };
    }

    private static bool EvaluateChanged(string key, string currentValue)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? "__default" : key.Trim();
        var existing = PreviousValues.TryGetValue(normalizedKey, out var previousValue);
        PreviousValues[normalizedKey] = currentValue;
        if (!existing)
            return false;

        return !string.Equals(previousValue, currentValue, StringComparison.Ordinal);
    }

    private static bool CompareNumeric(string left, string right, string comparison)
    {
        if (!double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftValue))
            return false;
        if (!double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightValue))
            return false;

        return comparison == "greater" ? leftValue > rightValue : leftValue < rightValue;
    }

    private static bool SafeRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            return false;
        }
    }

    private static string GetString(JsonElement element, string propertyName, string fallback)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? fallback;

        return value.ToString();
    }
}
