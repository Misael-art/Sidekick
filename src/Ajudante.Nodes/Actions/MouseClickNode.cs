using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.mouseClick",
    DisplayName = "Mouse Click",
    Category = NodeCategory.Action,
    Description = "Clicks the mouse at specified coordinates",
    Color = "#22C55E")]
public class MouseClickNode : IActionNode
{
    private int _x;
    private int _y;
    private string _button = "Left";
    private string _clickType = "Single";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.mouseClick",
        DisplayName = "Mouse Click",
        Category = NodeCategory.Action,
        Description = "Clicks the mouse at specified coordinates",
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
                Description = "X coordinate (supports {{variable}} templates)"
            },
            new()
            {
                Id = "y",
                Name = "Y",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Y coordinate (supports {{variable}} templates)"
            },
            new()
            {
                Id = "button",
                Name = "Button",
                Type = PropertyType.Dropdown,
                DefaultValue = "Left",
                Description = "Which mouse button to click",
                Options = new[] { "Left", "Right", "Middle" }
            },
            new()
            {
                Id = "clickType",
                Name = "Click Type",
                Type = PropertyType.Dropdown,
                DefaultValue = "Single",
                Description = "Single or double click",
                Options = new[] { "Single", "Double" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("x", out var x))
            _x = Convert.ToInt32(x);
        if (properties.TryGetValue("y", out var y))
            _y = Convert.ToInt32(y);
        if (properties.TryGetValue("button", out var btn) && btn is string button)
            _button = button;
        if (properties.TryGetValue("clickType", out var ct) && ct is string clickType)
            _clickType = clickType;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var x = ResolveCoordinate(context, "x", _x);
            var y = ResolveCoordinate(context, "y", _y);

            MouseSimulator.MoveTo(x, y);

            if (_clickType == "Double")
            {
                MouseSimulator.DoubleClick();
            }
            else if (_button == "Right")
            {
                MouseSimulator.RightClick();
            }
            else
            {
                var mouseButton = _button switch
                {
                    "Right" => MouseButton.Right,
                    "Middle" => MouseButton.Middle,
                    _ => MouseButton.Left
                };
                MouseSimulator.Click(mouseButton);
            }

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["x"] = x,
                ["y"] = y
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Mouse click failed: {ex.Message}"));
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
