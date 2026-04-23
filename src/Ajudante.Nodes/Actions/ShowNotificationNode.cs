using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Notifications;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.showNotification",
    DisplayName = "Show Notification",
    Category = NodeCategory.Action,
    Description = "Shows a desktop notification on Windows",
    Color = "#22C55E")]
public class ShowNotificationNode : IActionNode
{
    private string _title = "";
    private string _message = "";
    private int _timeoutMs = 3000;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.showNotification",
        DisplayName = "Show Notification",
        Category = NodeCategory.Action,
        Description = "Shows a desktop notification on Windows",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "title", Name = "Title", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "title", Name = "Title", Type = PropertyType.String, DefaultValue = "", Description = "Notification title" },
            new() { Id = "message", Name = "Message", Type = PropertyType.String, DefaultValue = "", Description = "Notification message" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 3000, Description = "Requested notification timeout" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _title = NodeValueHelper.GetString(properties, "title");
        _message = NodeValueHelper.GetString(properties, "message");
        _timeoutMs = NodeValueHelper.GetInt(properties, "timeoutMs", 3000);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var title = context.ResolveTemplate(_title);
        var message = context.ResolveTemplate(_message);
        await DesktopNotificationService.ShowAsync(title, message, _timeoutMs, ct);

        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["title"] = title,
            ["message"] = message
        });
    }
}
