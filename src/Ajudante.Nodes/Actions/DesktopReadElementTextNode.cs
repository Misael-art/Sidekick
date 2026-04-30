using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.desktopReadElementText",
    DisplayName = "Desktop Read Element Text",
    Category = NodeCategory.Action,
    Description = "Reads text from a Windows desktop element using UIAutomation",
    Color = "#22C55E")]
public class DesktopReadElementTextNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.desktopReadElementText",
        DisplayName = "Desktop Read Element Text",
        Category = NodeCategory.Action,
        Description = "Reads text from a Windows desktop element using UIAutomation",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Read", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "text", Name = "Text", DataType = PortDataType.String }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions()
            .Concat(new[]
            {
                new PropertyDefinition { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the extracted text" }
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
        context.EmitPhase(RuntimePhases.WaitingForElement, "Waiting for element text");
        var element = BrowserSelectorHelper.FindElement(selector);
        if (element is null)
        {
            if (selector.useRelativeFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "relative fallback active");
            else if (selector.useScaledFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "scaled fallback active");
            else if (selector.useAbsoluteFallback)
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "absolute fallback active");
            else
                context.EmitPhase(RuntimePhases.Error, "notFound");

            NodeValueHelper.SetVariableIfRequested(context, variableName, "");
            return Task.FromResult(NodeResult.Ok("notFound", new Dictionary<string, object?>
            {
                ["text"] = "",
                ["reason"] = "Element not found"
            }));
        }

        var text = AutomationElementLocator.ExtractText(element);
        context.EmitPhase(RuntimePhases.ElementMatched, "Element text read");
        NodeValueHelper.SetVariableIfRequested(context, variableName, text);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["text"] = text
        }));
    }
}
