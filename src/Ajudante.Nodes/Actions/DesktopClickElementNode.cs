using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Input;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.desktopClickElement",
    DisplayName = "Desktop Click Element",
    Category = NodeCategory.Action,
    Description = "Finds a Windows desktop element and clicks it with selector-first fallback",
    Color = "#22C55E")]
public class DesktopClickElementNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.desktopClickElement",
        DisplayName = "Desktop Click Element",
        Category = NodeCategory.Action,
        Description = "Finds a Windows desktop element and clicks it with selector-first fallback",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Clicked", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "clickedName", Name = "Clicked Name", DataType = PortDataType.String },
            new() { Id = "fallbackUsed", Name = "Fallback Used", DataType = PortDataType.Boolean }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions("button")
            .Concat(new[]
            {
                new PropertyDefinition { Id = "clickType", Name = "Click Type", Type = PropertyType.Dropdown, DefaultValue = "single", Description = "Single or double click", Options = new[] { "single", "double" } }
            })
            .ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var clickType = NodeValueHelper.GetString(_properties, "clickType", "single");
        context.EmitPhase(RuntimePhases.WaitingForElement, "Waiting for element before click");
        var element = BrowserSelectorHelper.FindElement(selector);
        if (element is null)
        {
            return Task.FromResult(NodeResult.Ok("notFound", new Dictionary<string, object?>
            {
                ["clicked"] = false,
                ["reason"] = "Element not found"
            }));
        }

        var fallbackUsed = false;
        context.EmitPhase(RuntimePhases.ElementMatched, "Element matched for click");
        if (!AutomationElementLocator.Invoke(element))
        {
            fallbackUsed = true;
            context.EmitPhase(RuntimePhases.FallbackVisualActive, "InvokePattern unavailable; using coordinate click");
            var rect = element.Current.BoundingRectangle;
            var centerX = (int)(rect.Left + rect.Width / 2);
            var centerY = (int)(rect.Top + rect.Height / 2);
            MouseSimulator.MoveTo(centerX, centerY);
            Thread.Sleep(100);
            if (string.Equals(clickType, "double", StringComparison.OrdinalIgnoreCase))
                MouseSimulator.DoubleClick();
            else
                MouseSimulator.Click();
        }

        context.EmitPhase(RuntimePhases.ClickExecuted, "Desktop click executed", new { fallbackUsed });
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["clickedName"] = element.Current.Name,
            ["fallbackUsed"] = fallbackUsed
        }));
    }
}
