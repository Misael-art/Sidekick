using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.compareText",
    DisplayName = "Compare Text",
    Category = NodeCategory.Logic,
    Description = "Compares two text values and branches based on the result",
    Color = "#EAB308")]
public class CompareTextNode : ILogicNode
{
    private string _text1 = "";
    private string _text2 = "";
    private string _comparison = "Equals";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.compareText",
        DisplayName = "Compare Text",
        Category = NodeCategory.Logic,
        Description = "Compares two text values and branches based on the result",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "match", Name = "Match", DataType = PortDataType.Flow },
            new() { Id = "noMatch", Name = "No Match", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "text1",
                Name = "Text 1",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "First text value (supports {{variable}} templates)"
            },
            new()
            {
                Id = "text2",
                Name = "Text 2",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "Second text value (supports {{variable}} templates)"
            },
            new()
            {
                Id = "comparison",
                Name = "Comparison",
                Type = PropertyType.Dropdown,
                DefaultValue = "Equals",
                Description = "How to compare the two texts",
                Options = new[] { "Equals", "Contains", "StartsWith", "EndsWith" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("text1", out var t1) && t1 is string text1)
            _text1 = text1;
        if (properties.TryGetValue("text2", out var t2) && t2 is string text2)
            _text2 = text2;
        if (properties.TryGetValue("comparison", out var c) && c is string comp)
            _comparison = comp;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedText1 = context.ResolveTemplate(_text1);
        var resolvedText2 = context.ResolveTemplate(_text2);

        var isMatch = _comparison switch
        {
            "Equals" => string.Equals(resolvedText1, resolvedText2, StringComparison.OrdinalIgnoreCase),
            "Contains" => resolvedText1.Contains(resolvedText2, StringComparison.OrdinalIgnoreCase),
            "StartsWith" => resolvedText1.StartsWith(resolvedText2, StringComparison.OrdinalIgnoreCase),
            "EndsWith" => resolvedText1.EndsWith(resolvedText2, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        return Task.FromResult(NodeResult.Ok(isMatch ? "match" : "noMatch"));
    }
}
