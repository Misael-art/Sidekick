using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Input;
using Ajudante.Platform.Screen;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.clickImageMatch",
    DisplayName = "Click Image Match",
    Category = NodeCategory.Action,
    Description = "Finds an image on screen and clicks its center as an explicit visual fallback",
    Color = "#22C55E")]
public class ClickImageMatchNode : IActionNode
{
    private byte[]? _templateImage;
    private double _threshold = 0.8;
    private int _timeoutMs = 5000;
    private int _intervalMs = 300;
    private string _clickType = "single";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.clickImageMatch",
        DisplayName = "Click Image Match",
        Category = NodeCategory.Action,
        Description = "Finds an image on screen and clicks its center as an explicit visual fallback",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Clicked", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "x", Name = "X", DataType = PortDataType.Number },
            new() { Id = "y", Name = "Y", DataType = PortDataType.Number },
            new() { Id = "confidence", Name = "Confidence", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "templateImage", Name = "Template Image", Type = PropertyType.ImageTemplate, Description = "Snip asset or inline image to match" },
            new() { Id = "threshold", Name = "Threshold", Type = PropertyType.Float, DefaultValue = 0.8, Description = "Minimum match confidence" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum time to search" },
            new() { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 300, Description = "Delay between retries" },
            new() { Id = "clickType", Name = "Click Type", Type = PropertyType.Dropdown, DefaultValue = "single", Description = "Single or double click", Options = new[] { "single", "double" } }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("templateImage", out var img))
            _templateImage = ImageTemplateResolver.Resolve(img);

        if (properties.TryGetValue("threshold", out var threshold))
            _threshold = Convert.ToDouble(threshold);

        _timeoutMs = Math.Max(0, NodeValueHelper.GetInt(properties, "timeoutMs", 5000));
        _intervalMs = Math.Max(100, NodeValueHelper.GetInt(properties, "intervalMs", 300));
        _clickType = NodeValueHelper.GetString(properties, "clickType", "single");
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        if (_templateImage is null || _templateImage.Length == 0)
            return NodeResult.Fail("Click Image Match requires a template image.");

        context.EmitPhase(RuntimePhases.FallbackVisualActive, "Searching screen by image template");
        var startedAt = Environment.TickCount64;
        do
        {
            ct.ThrowIfCancellationRequested();
            var match = TemplateMatching.FindOnScreen(_templateImage, _threshold);
            if (match is not null)
            {
                context.EmitPhase(RuntimePhases.ElementMatched, "Image template matched", new { match.Confidence });
                MouseSimulator.MoveTo(match.Center.X, match.Center.Y);
                Thread.Sleep(80);
                if (string.Equals(_clickType, "double", StringComparison.OrdinalIgnoreCase))
                    MouseSimulator.DoubleClick();
                else
                    MouseSimulator.Click();

                context.EmitPhase(RuntimePhases.ClickExecuted, "Image match click executed", new { match.Center.X, match.Center.Y, match.Confidence });
                return NodeResult.Ok("out", new Dictionary<string, object?>
                {
                    ["x"] = match.Center.X,
                    ["y"] = match.Center.Y,
                    ["confidence"] = match.Confidence,
                    ["fallbackUsed"] = true
                });
            }

            if (_timeoutMs <= 0)
                break;

            await Task.Delay(_intervalMs, ct);
        }
        while (Environment.TickCount64 - startedAt < _timeoutMs);

        return NodeResult.Ok("notFound", new Dictionary<string, object?>
        {
            ["reason"] = "Image not found within timeout",
            ["fallbackUsed"] = true
        });
    }
}
