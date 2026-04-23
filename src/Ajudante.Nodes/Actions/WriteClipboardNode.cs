using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Clipboard;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.writeClipboard",
    DisplayName = "Clipboard Write",
    Category = NodeCategory.Action,
    Description = "Writes text to the system clipboard",
    Color = "#22C55E")]
public class WriteClipboardNode : IActionNode
{
    private string _text = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.writeClipboard",
        DisplayName = "Clipboard Write",
        Category = NodeCategory.Action,
        Description = "Writes text to the system clipboard",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "text", Name = "Text", Type = PropertyType.String, DefaultValue = "", Description = "Text to place on the clipboard (supports {{variable}} templates)" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _text = NodeValueHelper.GetString(properties, "text");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolved = context.ResolveTemplate(_text);
        ClipboardService.SetText(resolved);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["text"] = resolved
        }));
    }
}
