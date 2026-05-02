using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Logic;

[NodeInfo(TypeId = "logic.untilDateTime", DisplayName = "Until Date/Time", Category = NodeCategory.Logic, Color = "#EAB308", Description = "Routes before/after a local wall-clock time such as midnight")]
public sealed class UntilDateTimeNode : ILogicNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.untilDateTime",
        DisplayName = "Until Date/Time",
        Category = NodeCategory.Logic,
        Color = "#EAB308",
        Description = "Compares now against a local time and returns remaining milliseconds",
        InputPorts = FlowInput(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "before", Name = "Before", DataType = PortDataType.Flow },
            new() { Id = "after", Name = "After", DataType = PortDataType.Flow },
            new() { Id = "remainingMs", Name = "Remaining (ms)", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "untilLocalTime", Name = "Until Local Time", Type = PropertyType.String, DefaultValue = "00:00" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var now = DateTime.Now;
        var until = ParseTodayOrTomorrow(NodeValueHelper.GetString(_properties, "untilLocalTime", "00:00"), now);
        var before = now < until;
        var remainingMs = before ? Math.Max(0, (int)(until - now).TotalMilliseconds) : 0;
        return Task.FromResult(NodeResult.Ok(before ? "before" : "after", new Dictionary<string, object?>
        {
            ["untilLocal"] = until.ToString("o"),
            ["remainingMs"] = remainingMs
        }));
    }

    private static DateTime ParseTodayOrTomorrow(string text, DateTime now)
    {
        if (!TimeSpan.TryParse(text, out var time))
            time = TimeSpan.Zero;

        var target = now.Date.Add(time);
        return target <= now ? target.AddDays(1) : target;
    }

    private static List<PortDefinition> FlowInput() => new()
    {
        new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
    };
}

[NodeInfo(TypeId = "logic.dailyReset", DisplayName = "Daily Reset", Category = NodeCategory.Logic, Color = "#EAB308", Description = "Routes reset/notReset based on local calendar day")]
public sealed class DailyResetNode : ILogicNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "logic.dailyReset",
        DisplayName = "Daily Reset",
        Category = NodeCategory.Logic,
        Color = "#EAB308",
        Description = "Computes whether a persisted yyyy-MM-dd value differs from today",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "reset", Name = "Reset", DataType = PortDataType.Flow },
            new() { Id = "notReset", Name = "Not Reset", DataType = PortDataType.Flow },
            new() { Id = "today", Name = "Today", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "lastDate", Name = "Last Date", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "storeTodayInVariable", Name = "Store Today In Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var lastDate = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "lastDate", ""));
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeTodayInVariable", ""), today);
        return Task.FromResult(NodeResult.Ok(string.Equals(lastDate, today, StringComparison.Ordinal) ? "notReset" : "reset", new Dictionary<string, object?>
        {
            ["today"] = today,
            ["lastDate"] = lastDate
        }));
    }
}
