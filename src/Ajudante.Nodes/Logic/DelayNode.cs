using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.delay",
    DisplayName = "Delay",
    Category = NodeCategory.Logic,
    Description = "Pauses execution for a specified duration",
    Color = "#EAB308")]
public class DelayNode : ILogicNode
{
    private int _milliseconds = 1000;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.delay",
        DisplayName = "Delay",
        Category = NodeCategory.Logic,
        Description = "Pauses execution for a specified duration",
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
                Id = "milliseconds",
                Name = "Delay (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 1000,
                Description = "Duration to wait in milliseconds"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("milliseconds", out var ms))
            _milliseconds = Convert.ToInt32(ms);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        await Task.Delay(_milliseconds, ct);
        return NodeResult.Ok("out");
    }
}
