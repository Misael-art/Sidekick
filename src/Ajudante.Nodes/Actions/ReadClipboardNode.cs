using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Clipboard;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.readClipboard",
    DisplayName = "Clipboard Read",
    Category = NodeCategory.Action,
    Description = "Reads text from the system clipboard",
    Color = "#22C55E")]
public class ReadClipboardNode : IActionNode
{
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.readClipboard",
        DisplayName = "Clipboard Read",
        Category = NodeCategory.Action,
        Description = "Reads text from the system clipboard",
        Color = "#22C55E",
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
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the clipboard text" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var text = ClipboardService.GetText();
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, text);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["text"] = text
        }));
    }
}
