using System.Text.Json;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(TypeId = "action.persistState", DisplayName = "Persist State", Category = NodeCategory.Action, Color = "#22C55E", Description = "Persists a small flow state value under AppData/Sidekick/state")]
public sealed class PersistStateNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.persistState",
        DisplayName = "Persist State",
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = "Persists a key/value used by blocking/cooldown flows",
        InputPorts = FlowInput(),
        OutputPorts = FlowOutput(),
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "key", Name = "Key", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "value", Name = "Value", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "expiresAtLocal", Name = "Expires At Local", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var key = NodeValueHelper.ResolveTemplateProperty(context, _properties, "key", "");
        if (string.IsNullOrWhiteSpace(key))
            return NodeResult.Fail("Persist State requer key.");

        var value = NodeValueHelper.ResolveTemplateProperty(context, _properties, "value", "");
        var expiresAt = NodeValueHelper.ResolveTemplateProperty(context, _properties, "expiresAtLocal", "");
        var record = new PersistedStateRecord
        {
            Key = key,
            Value = value,
            ExpiresAtLocal = expiresAt,
            UpdatedAtUtc = DateTime.UtcNow
        };

        Directory.CreateDirectory(StateDirectory);
        await File.WriteAllTextAsync(GetStatePath(key), JsonSerializer.Serialize(record, JsonOptions), ct);
        return NodeResult.Ok("out", new Dictionary<string, object?> { ["key"] = key, ["value"] = value, ["expiresAtLocal"] = expiresAt });
    }

    internal static string StateDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidekick", "state");
    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static string GetStatePath(string key)
    {
        var safe = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        return Path.Combine(StateDirectory, $"{safe}.json");
    }

    internal static List<PortDefinition> FlowInput() => new()
    {
        new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
    };

    internal static List<PortDefinition> FlowOutput(params PortDefinition[] extra) =>
        new[] { new PortDefinition { Id = "out", Name = "Out", DataType = PortDataType.Flow } }.Concat(extra).ToList();
}

[NodeInfo(TypeId = "action.readState", DisplayName = "Read State", Category = NodeCategory.Action, Color = "#22C55E", Description = "Reads state persisted by Persist State")]
public sealed class ReadStateNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.readState",
        DisplayName = "Read State",
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = "Reads persisted state and routes found/missing",
        InputPorts = PersistStateNode.FlowInput(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "found", Name = "Found", DataType = PortDataType.Flow },
            new() { Id = "missing", Name = "Missing", DataType = PortDataType.Flow },
            new() { Id = "value", Name = "Value", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "key", Name = "Key", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var key = NodeValueHelper.ResolveTemplateProperty(context, _properties, "key", "");
        if (string.IsNullOrWhiteSpace(key))
            return NodeResult.Fail("Read State requer key.");

        var path = PersistStateNode.GetStatePath(key);
        if (!File.Exists(path))
            return NodeResult.Ok("missing", new Dictionary<string, object?> { ["key"] = key, ["exists"] = false });

        var json = await File.ReadAllTextAsync(path, ct);
        var record = JsonSerializer.Deserialize<PersistedStateRecord>(json, PersistStateNode.JsonOptions);
        var value = record?.Value ?? "";
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeInVariable", ""), value);
        return NodeResult.Ok("found", new Dictionary<string, object?> { ["key"] = key, ["value"] = value, ["expiresAtLocal"] = record?.ExpiresAtLocal });
    }
}

internal sealed class PersistedStateRecord
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string ExpiresAtLocal { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
