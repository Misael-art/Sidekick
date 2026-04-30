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
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "found", Name = "Found", DataType = PortDataType.Boolean }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions()
            .Concat(new[]
            {
                new PropertyDefinition { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable for the found flag" }
            })
            .ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = properties;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var variableName = NodeValueHelper.GetString(_properties, "storeInVariable");
        var element = BrowserSelectorHelper.FindElement(selector);
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
