using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.cooldown",
    DisplayName = "Cooldown",
    Category = NodeCategory.Logic,
    Description = "Limits how often a branch continues, useful for preventing click loops",
    Color = "#EAB308")]
public class CooldownNode : ILogicNode
{
    private string _key = "default";
    private int _intervalMs = 1500;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.cooldown",
        DisplayName = "Cooldown",
        Category = NodeCategory.Logic,
        Description = "Limits how often a branch continues, useful for preventing click loops",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "passthrough", Name = "Pass Through", DataType = PortDataType.Flow },
            new() { Id = "cooldown", Name = "Cooldown Active", DataType = PortDataType.Flow },
            new() { Id = "remainingMs", Name = "Remaining (ms)", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "key", Name = "Cooldown Key", Type = PropertyType.String, DefaultValue = "default", Description = "Independent counters share state when they share a key" },
            new() { Id = "intervalMs", Name = "Interval (ms)", Type = PropertyType.Integer, DefaultValue = 1500, Description = "Minimum time between pass-throughs" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _key = NodeValueHelper.GetString(properties, "key", "default");
        _intervalMs = Math.Max(0, NodeValueHelper.GetInt(properties, "intervalMs", 1500));
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var key = context.ResolveTemplate(_key);
        var stateKey = $"__cooldown_{key}_lastTickMs";
        var now = Environment.TickCount64;
        var last = context.GetVariable<long>(stateKey);
        var elapsed = now - last;
        var remaining = (int)Math.Max(0, _intervalMs - elapsed);

        if (last > 0 && elapsed < _intervalMs)
        {
            context.EmitPhase(RuntimePhases.CooldownActive, $"Cooldown active for {remaining}ms");
            return Task.FromResult(NodeResult.Ok("cooldown", new Dictionary<string, object?>
            {
                ["key"] = key,
                ["remainingMs"] = remaining
            }));
        }

        context.SetVariable(stateKey, now);
        return Task.FromResult(NodeResult.Ok("passthrough", new Dictionary<string, object?>
        {
            ["key"] = key,
            ["remainingMs"] = 0
        }));
    }
}
