using System.Globalization;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(
    TypeId = "logic.delay",
    DisplayName = "Delay",
    Category = NodeCategory.Logic,
    Description = "Pauses execution for a specified duration",
    Color = "#EAB308")]
public class DelayNode : ILogicNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
    private int _fallbackMilliseconds = 1000;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.delay",
        DisplayName = "Delay",
        Category = NodeCategory.Logic,
        Description = "Pauses execution for a specified duration",
        Color = "#EAB308",
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
            new()
            {
                Id = "milliseconds",
                Name = "Delay (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 1000,
                Description = "Duration to wait in milliseconds"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        var raw = NodeValueHelper.GetString(_properties, "milliseconds", "1000");
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            _fallbackMilliseconds = parsed;
        else
            _fallbackMilliseconds = 1000;
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var raw = NodeValueHelper.GetString(
            _properties,
            "milliseconds",
            _fallbackMilliseconds.ToString(CultureInfo.InvariantCulture));
        var resolved = context.ResolveTemplate(raw).Trim();
        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) || ms < 0)
            ms = _fallbackMilliseconds;

        await Task.Delay(ms, ct);
        return NodeResult.Ok("out");
    }
}
