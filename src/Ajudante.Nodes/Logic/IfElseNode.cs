using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.ifElse",
    DisplayName = "If / Else",
    Category = NodeCategory.Logic,
    Description = "Branches execution based on a condition",
    Color = "#EAB308")]
public class IfElseNode : ILogicNode
{
    private string _conditionType = "VariableEquals";
    private string _variableName = "";
    private string _compareValue = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.ifElse",
        DisplayName = "If / Else",
        Category = NodeCategory.Logic,
        Description = "Branches execution based on a condition",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "true", Name = "True", DataType = PortDataType.Flow },
            new() { Id = "false", Name = "False", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "conditionType",
                Name = "Condition Type",
                Type = PropertyType.Dropdown,
                DefaultValue = "VariableEquals",
                Description = "The type of condition to evaluate",
                Options = new[] { "VariableEquals", "VariableContains", "VariableGreaterThan", "PixelColorIs" }
            },
            new()
            {
                Id = "variableName",
                Name = "Variable Name",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The name of the variable to check"
            },
            new()
            {
                Id = "compareValue",
                Name = "Compare Value",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The value to compare against"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("conditionType", out var ct) && ct is string condType)
            _conditionType = condType;
        if (properties.TryGetValue("variableName", out var vn) && vn is string varName)
            _variableName = varName;
        if (properties.TryGetValue("compareValue", out var cv) && cv is string compVal)
            _compareValue = compVal;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedVarName = context.ResolveTemplate(_variableName);
        var resolvedCompareValue = context.ResolveTemplate(_compareValue);
        var variableValue = context.GetVariable(resolvedVarName)?.ToString() ?? "";

        var result = _conditionType switch
        {
            "VariableEquals" => string.Equals(variableValue, resolvedCompareValue, StringComparison.OrdinalIgnoreCase),
            "VariableContains" => variableValue.Contains(resolvedCompareValue, StringComparison.OrdinalIgnoreCase),
            "VariableGreaterThan" => CompareGreaterThan(variableValue, resolvedCompareValue),
            "PixelColorIs" => CheckPixelColor(resolvedVarName, resolvedCompareValue),
            _ => false
        };

        return Task.FromResult(NodeResult.Ok(result ? "true" : "false"));
    }

    private static bool CompareGreaterThan(string left, string right)
    {
        if (double.TryParse(left, out var leftNum) && double.TryParse(right, out var rightNum))
            return leftNum > rightNum;
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private bool CheckPixelColor(string coordsVariable, string expectedColor)
    {
        // For PixelColorIs, variableName is expected to contain "x,y" coordinates
        // and compareValue is the expected color
        try
        {
            var parts = coordsVariable.Split(',');
            if (parts.Length != 2) return false;

            var x = int.Parse(parts[0].Trim());
            var y = int.Parse(parts[1].Trim());

            var color = Ajudante.Platform.Screen.PixelReader.GetPixelColor(x, y);
            return color.ToString().Equals(expectedColor, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
