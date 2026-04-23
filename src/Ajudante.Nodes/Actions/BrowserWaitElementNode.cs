using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.browserWaitElement",
    DisplayName = "Browser Wait Element",
    Category = NodeCategory.Action,
    Description = "Waits until a browser UI element is found",
    Color = "#22C55E")]
public class BrowserWaitElementNode : IActionNode
{
    private Dictionary<string, object?> _properties = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.browserWaitElement",
        DisplayName = "Browser Wait Element",
        Category = NodeCategory.Action,
        Description = "Waits until a browser UI element is found",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "found", Name = "Found", DataType = PortDataType.Boolean }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "", Description = "Optional browser window title" },
            new() { Id = "automationId", Name = "Automation ID", Type = PropertyType.String, DefaultValue = "", Description = "Optional automation id" },
            new() { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "", Description = "Visible element name/text" },
            new() { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = "", Description = "Optional UIAutomation control type" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum wait time for the element" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable for the found flag" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = properties;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var variableName = NodeValueHelper.GetString(_properties, "storeInVariable");
        var element = AutomationElementLocator.FindElement(selector.windowTitle, selector.automationId, selector.elementName, selector.controlType, selector.timeoutMs);
        var found = element is not null;

        NodeValueHelper.SetVariableIfRequested(context, variableName, found);

        if (!found)
            return Task.FromResult(NodeResult.Fail("Browser element not found within timeout"));

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["found"] = true
        }));
    }
}
