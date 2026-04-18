using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.mouseDrag",
    DisplayName = "Mouse Drag",
    Category = NodeCategory.Action,
    Description = "Drags the mouse from one point to another",
    Color = "#22C55E")]
public class MouseDragNode : IActionNode
{
    private int _fromX;
    private int _fromY;
    private int _toX;
    private int _toY;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.mouseDrag",
        DisplayName = "Mouse Drag",
        Category = NodeCategory.Action,
        Description = "Drags the mouse from one point to another",
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
                Id = "fromX",
                Name = "From X",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Starting X coordinate"
            },
            new()
            {
                Id = "fromY",
                Name = "From Y",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Starting Y coordinate"
            },
            new()
            {
                Id = "toX",
                Name = "To X",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Ending X coordinate"
            },
            new()
            {
                Id = "toY",
                Name = "To Y",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Ending Y coordinate"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("fromX", out var fx))
            _fromX = Convert.ToInt32(fx);
        if (properties.TryGetValue("fromY", out var fy))
            _fromY = Convert.ToInt32(fy);
        if (properties.TryGetValue("toX", out var tx))
            _toX = Convert.ToInt32(tx);
        if (properties.TryGetValue("toY", out var ty))
            _toY = Convert.ToInt32(ty);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var fromX = ResolveCoordinate(context, "fromX", _fromX);
            var fromY = ResolveCoordinate(context, "fromY", _fromY);
            var toX = ResolveCoordinate(context, "toX", _toX);
            var toY = ResolveCoordinate(context, "toY", _toY);

            MouseSimulator.DragTo(fromX, fromY, toX, toY);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["fromX"] = fromX,
                ["fromY"] = fromY,
                ["toX"] = toX,
                ["toY"] = toY
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Mouse drag failed: {ex.Message}"));
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
