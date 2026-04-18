using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.getVariable",
    DisplayName = "Get Variable",
    Category = NodeCategory.Logic,
    Description = "Reads a variable from the execution context",
    Color = "#EAB308")]
public class GetVariableNode : ILogicNode
{
    private string _variableName = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.getVariable",
        DisplayName = "Get Variable",
        Category = NodeCategory.Logic,
        Description = "Reads a variable from the execution context",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "value", Name = "Value", DataType = PortDataType.Any }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "variableName",
                Name = "Variable Name",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The name of the variable to read"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("variableName", out var vn) && vn is string name)
            _variableName = name;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_variableName))
            return Task.FromResult(NodeResult.Fail("Variable name is required"));

        var resolvedName = context.ResolveTemplate(_variableName);
        var value = context.GetVariable(resolvedName);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["value"] = value
        }));
    }
}
