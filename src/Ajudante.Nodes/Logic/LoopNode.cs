using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.loop",
    DisplayName = "Loop",
    Category = NodeCategory.Logic,
    Description = "Repeats a block of nodes a specified number of times",
    Color = "#EAB308")]
public class LoopNode : ILogicNode
{
    private int _count = 5;
    private int _delayBetween;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.loop",
        DisplayName = "Loop",
        Category = NodeCategory.Logic,
        Description = "Repeats a block of nodes a specified number of times",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "body", Name = "Body", DataType = PortDataType.Flow },
            new() { Id = "done", Name = "Done", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "count",
                Name = "Iterations",
                Type = PropertyType.Integer,
                DefaultValue = 5,
                Description = "Number of times to repeat"
            },
            new()
            {
                Id = "delayBetween",
                Name = "Delay Between (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Delay in milliseconds between each iteration"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("count", out var c))
            _count = Convert.ToInt32(c);
        if (properties.TryGetValue("delayBetween", out var d))
            _delayBetween = Convert.ToInt32(d);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        for (var i = 0; i < _count; i++)
        {
            ct.ThrowIfCancellationRequested();

            context.SetVariable("loopIndex", i);
            context.SetVariable("loopIteration", i + 1);
            context.SetVariable("loopCount", _count);

            // Signal that the body should be executed for this iteration
            context.SetNodeOutputs(Id, new Dictionary<string, object?>
            {
                ["loopIndex"] = i,
                ["loopIteration"] = i + 1,
                ["loopCount"] = _count
            });

            // Return "body" for each iteration except the last
            // The executor will follow the "body" output connections
            if (i < _count - 1 && _delayBetween > 0)
            {
                await Task.Delay(_delayBetween, ct);
            }
        }

        // After all iterations, continue via "done" output
        return NodeResult.Ok("done", new Dictionary<string, object?>
        {
            ["loopIndex"] = _count - 1,
            ["totalIterations"] = _count
        });
    }
}
