using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.browserExtractText",
    DisplayName = "Browser Extract Text",
    Category = NodeCategory.Action,
    Description = "Reads text from a browser UI element",
    Color = "#22C55E")]
public class BrowserExtractTextNode : IActionNode
{
    private Dictionary<string, object?> _properties = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.browserExtractText",
        DisplayName = "Browser Extract Text",
        Category = NodeCategory.Action,
        Description = "Reads text from a browser UI element",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
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
        _properties = properties;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var variableName = NodeValueHelper.GetString(_properties, "storeInVariable");
        var element = BrowserSelectorHelper.FindElement(selector);
        if (element is null)
            return Task.FromResult(NodeResult.Fail("Browser element not found"));

        var text = AutomationElementLocator.ExtractText(element);
        NodeValueHelper.SetVariableIfRequested(context, variableName, text);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["text"] = text
        }));
    }
}
