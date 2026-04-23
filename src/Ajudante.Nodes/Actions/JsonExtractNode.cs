using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.jsonExtract",
    DisplayName = "JSON Extract",
    Category = NodeCategory.Action,
    Description = "Extracts a value from JSON using dot-path syntax",
    Color = "#22C55E")]
public class JsonExtractNode : IActionNode
{
    private string _json = "";
    private string _path = "";
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.jsonExtract",
        DisplayName = "JSON Extract",
        Category = NodeCategory.Action,
        Description = "Extracts a value from JSON using dot-path syntax",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "value", Name = "Value", DataType = PortDataType.Any }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "json", Name = "JSON", Type = PropertyType.String, DefaultValue = "", Description = "JSON content (supports {{variable}} templates)" },
            new() { Id = "path", Name = "Path", Type = PropertyType.String, DefaultValue = "", Description = "Dot path such as data.items[0].name" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the extracted value" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _json = NodeValueHelper.GetString(properties, "json");
        _path = NodeValueHelper.GetString(properties, "path");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedJson = context.ResolveTemplate(_json);
        if (string.IsNullOrWhiteSpace(resolvedJson))
            return Task.FromResult(NodeResult.Fail("JSON input is required"));

        try
        {
            var value = JsonPathHelper.ReadValue(resolvedJson, _path);
            NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, value);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["value"] = value
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail(ex.Message));
        }
    }
}
