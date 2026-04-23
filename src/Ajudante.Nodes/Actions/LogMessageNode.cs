using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.logMessage",
    DisplayName = "Log",
    Category = NodeCategory.Action,
    Description = "Writes a message to node outputs and optionally to a variable",
    Color = "#22C55E")]
public class LogMessageNode : IActionNode
{
    private string _message = "";
    private string _level = "info";
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.logMessage",
        DisplayName = "Log",
        Category = NodeCategory.Action,
        Description = "Writes a message to node outputs and optionally to a variable",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "message", Name = "Message", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "message", Name = "Message", Type = PropertyType.String, DefaultValue = "", Description = "Log message (supports {{variable}} templates)" },
            new() { Id = "level", Name = "Level", Type = PropertyType.Dropdown, DefaultValue = "info", Description = "Semantic log level", Options = new[] { "info", "warning", "error", "debug" } },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable name to receive the resolved message" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _message = NodeValueHelper.GetString(properties, "message");
        _level = NodeValueHelper.GetString(properties, "level", "info");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolved = context.ResolveTemplate(_message);
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, resolved);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["message"] = resolved,
            ["level"] = _level
        }));
    }
}
