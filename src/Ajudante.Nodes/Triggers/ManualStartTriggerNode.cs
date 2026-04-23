using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.manualStart",
    DisplayName = "Start Manual",
    Category = NodeCategory.Trigger,
    Description = "Explicit flow entry point for manually started automations",
    Color = "#EF4444")]
public class ManualStartTriggerNode : ITriggerNode
{
    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.manualStart",
        DisplayName = "Start Manual",
        Category = NodeCategory.Trigger,
        Description = "Explicit flow entry point for manually started automations",
        Color = "#EF4444",
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopWatchingAsync() => Task.CompletedTask;
}
