using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.desktopWaitElement",
    DisplayName = "Desktop Wait Element",
    Category = NodeCategory.Action,
    Description = "Waits for a Windows desktop element using UIAutomation selectors",
    Color = "#22C55E")]
public class DesktopWaitElementNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.desktopWaitElement",
        DisplayName = "Desktop Wait Element",
        Category = NodeCategory.Action,
        Description = "Waits for a Windows desktop element using UIAutomation selectors",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Found", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "found", Name = "Found Flag", DataType = PortDataType.Boolean }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions()
            .Concat(new[]
            {
                new PropertyDefinition { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive true/false" }
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
        var variableName = NodeValueHelper.GetString(_properties, "storeInVariable");
        context.EmitPhase(RuntimePhases.WaitingForElement, "Waiting for desktop element");
        var element = BrowserSelectorHelper.FindElement(selector);
        var found = element is not null;
        NodeValueHelper.SetVariableIfRequested(context, variableName, found);
        if (found)
            context.EmitPhase(RuntimePhases.ElementMatched, "Desktop element matched");
        else
        {
            if (selector.useRelativeFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "relative fallback active");
            else if (selector.useScaledFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "scaled fallback active");
            else if (selector.useAbsoluteFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "absolute fallback active");
            else
                context.EmitPhase(RuntimePhases.Error, "notFound");
        }

        return Task.FromResult(found
            ? NodeResult.Ok("out", new Dictionary<string, object?> { ["found"] = true })
            : NodeResult.Ok("notFound", new Dictionary<string, object?>
            {
                ["found"] = false,
                ["reason"] = "Element not found within timeout"
            }));
    }
}
