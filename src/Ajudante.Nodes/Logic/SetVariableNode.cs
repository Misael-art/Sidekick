using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.setVariable",
    DisplayName = "Set Variable",
    Category = NodeCategory.Logic,
    Description = "Sets a variable in the execution context",
    Color = "#EAB308")]
public class SetVariableNode : ILogicNode
{
    private string _variableName = "";
    private string _value = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.setVariable",
        DisplayName = "Set Variable",
        Category = NodeCategory.Logic,
        Description = "Sets a variable in the execution context",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "variableName",
                Name = "Variable Name",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The name of the variable to set"
            },
            new()
            {
                Id = "value",
                Name = "Value",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The value to assign (supports {{variable}} templates)"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("variableName", out var vn) && vn is string name)
            _variableName = name;
        if (properties.TryGetValue("value", out var v) && v is string val)
            _value = val;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_variableName))
            return Task.FromResult(NodeResult.Fail("Variable name is required"));

        var resolvedValue = context.ResolveTemplate(_value);
        context.SetVariable(_variableName, resolvedValue);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["variableName"] = _variableName,
            ["value"] = resolvedValue
        }));
    }
}
