using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.retryFlow",
    DisplayName = "Retry Flow",
    Category = NodeCategory.Logic,
    Description = "Counts retry attempts and routes to retry or give up",
    Color = "#EAB308")]
public class RetryFlowNode : ILogicNode
{
    private string _counterVariable = "retryCount";
    private int _maxAttempts = 3;
    private int _delayMs;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.retryFlow",
        DisplayName = "Retry Flow",
        Category = NodeCategory.Logic,
        Description = "Counts retry attempts and routes to retry or give up",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "retry", Name = "Retry", DataType = PortDataType.Flow },
            new() { Id = "giveUp", Name = "Give Up", DataType = PortDataType.Flow },
            new() { Id = "attempt", Name = "Attempt", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "counterVariable", Name = "Counter Variable", Type = PropertyType.String, DefaultValue = "retryCount", Description = "Variable used to track attempts" },
            new() { Id = "maxAttempts", Name = "Max Attempts", Type = PropertyType.Integer, DefaultValue = 3, Description = "Maximum retry attempts before giving up" },
            new() { Id = "delayMs", Name = "Delay (ms)", Type = PropertyType.Integer, DefaultValue = 0, Description = "Optional delay before retrying" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _counterVariable = NodeValueHelper.GetString(properties, "counterVariable", "retryCount");
        _maxAttempts = Math.Max(1, NodeValueHelper.GetInt(properties, "maxAttempts", 3));
        _delayMs = Math.Max(0, NodeValueHelper.GetInt(properties, "delayMs", 0));
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var currentAttempt = context.GetVariable<int>(_counterVariable);
        currentAttempt++;
        context.SetVariable(_counterVariable, currentAttempt);

        if (currentAttempt <= _maxAttempts)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, ct);

            return NodeResult.Ok("retry", new Dictionary<string, object?>
            {
                ["attempt"] = currentAttempt,
                ["remaining"] = _maxAttempts - currentAttempt
            });
        }

        return NodeResult.Ok("giveUp", new Dictionary<string, object?>
        {
            ["attempt"] = currentAttempt,
            ["remaining"] = 0
        });
    }
}
