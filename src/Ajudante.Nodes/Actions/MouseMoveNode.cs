using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.mouseMove",
    DisplayName = "Mouse Move",
    Category = NodeCategory.Action,
    Description = "Moves the mouse cursor to specified coordinates",
    Color = "#22C55E")]
public class MouseMoveNode : IActionNode
{
    private int _x;
    private int _y;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.mouseMove",
        DisplayName = "Mouse Move",
        Category = NodeCategory.Action,
        Description = "Moves the mouse cursor to specified coordinates",
        Color = "#22C55E",
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
                Id = "x",
                Name = "X",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "X coordinate to move to"
            },
            new()
            {
                Id = "y",
                Name = "Y",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Y coordinate to move to"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("x", out var x))
            _x = Convert.ToInt32(x);
        if (properties.TryGetValue("y", out var y))
            _y = Convert.ToInt32(y);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var x = ResolveCoordinate(context, "x", _x);
            var y = ResolveCoordinate(context, "y", _y);

            MouseSimulator.MoveTo(x, y);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["x"] = x,
                ["y"] = y
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Mouse move failed: {ex.Message}"));
        }
    }

    private static int ResolveCoordinate(FlowExecutionContext context, string name, int fallback)
    {
        var variable = context.GetVariable(name);
        if (variable != null && int.TryParse(variable.ToString(), out var resolved))
            return resolved;
        return fallback;
    }
}
