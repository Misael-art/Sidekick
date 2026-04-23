using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.textTemplate",
    DisplayName = "Text Template",
    Category = NodeCategory.Logic,
    Description = "Builds text from variables and node outputs",
    Color = "#EAB308")]
public class TextTemplateNode : ILogicNode
{
    private string _template = "";
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.textTemplate",
        DisplayName = "Text Template",
        Category = NodeCategory.Logic,
        Description = "Builds text from variables and node outputs",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "text", Name = "Text", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "template", Name = "Template", Type = PropertyType.String, DefaultValue = "", Description = "Template text using {{variable}} or {{nodeId.outputId}}" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the rendered text" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _template = NodeValueHelper.GetString(properties, "template");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var rendered = context.ResolveTemplate(_template);
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, rendered);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["text"] = rendered
        }));
    }
}
