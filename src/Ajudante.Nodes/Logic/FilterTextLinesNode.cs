using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.filterTextLines",
    DisplayName = "Filter Text Lines",
    Category = NodeCategory.Logic,
    Description = "Filters lines of text using simple match rules",
    Color = "#EAB308")]
public class FilterTextLinesNode : ILogicNode
{
    private string _input = "";
    private string _pattern = "";
    private string _mode = "contains";
    private bool _caseSensitive;
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.filterTextLines",
        DisplayName = "Filter Text Lines",
        Category = NodeCategory.Logic,
        Description = "Filters lines of text using simple match rules",
        Color = "#EAB308",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "filteredText", Name = "Filtered Text", DataType = PortDataType.String },
            new() { Id = "count", Name = "Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "input", Name = "Input", Type = PropertyType.String, DefaultValue = "", Description = "Input text containing one item per line" },
            new() { Id = "pattern", Name = "Pattern", Type = PropertyType.String, DefaultValue = "", Description = "Text pattern to match" },
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "contains", Description = "Match rule", Options = new[] { "contains", "startsWith", "endsWith", "equals" } },
            new() { Id = "caseSensitive", Name = "Case Sensitive", Type = PropertyType.Boolean, DefaultValue = false, Description = "Use case sensitive comparisons" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the filtered text" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _input = NodeValueHelper.GetString(properties, "input");
        _pattern = NodeValueHelper.GetString(properties, "pattern");
        _mode = NodeValueHelper.GetString(properties, "mode", "contains");
        _caseSensitive = NodeValueHelper.GetBool(properties, "caseSensitive");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var pattern = context.ResolveTemplate(_pattern);
        var lines = context.ResolveTemplate(_input)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        var filtered = lines.Where(line => Matches(line, pattern, comparison)).ToArray();
        var resultText = string.Join(Environment.NewLine, filtered);
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, resultText);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["filteredText"] = resultText,
            ["count"] = filtered.Length
        }));
    }

    private bool Matches(string line, string pattern, StringComparison comparison)
    {
        return _mode.ToLowerInvariant() switch
        {
            "startswith" => line.StartsWith(pattern, comparison),
            "endswith" => line.EndsWith(pattern, comparison),
            "equals" => string.Equals(line, pattern, comparison),
            _ => line.Contains(pattern, comparison)
        };
    }
}
