using System.Text.Json.Serialization;

namespace Ajudante.Core;

public class NodeDefinition
{
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public required NodeCategory Category { get; init; }
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#888888";
    public List<PortDefinition> InputPorts { get; init; } = new();
    public List<PortDefinition> OutputPorts { get; init; } = new();
    public List<PropertyDefinition> Properties { get; init; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeCategory
{
    Trigger,
    Logic,
    Action
}

public class PortDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public PortDataType DataType { get; init; } = PortDataType.Flow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortDataType
{
    Flow,
    String,
    Number,
    Boolean,
    Point,
    Image,
    Any
}

public class PropertyDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required PropertyType Type { get; init; }
    public object? DefaultValue { get; init; }
    public string? Description { get; init; }
    public string[]? Options { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    String,
    Integer,
    Float,
    Boolean,
    FilePath,
    FolderPath,
    Hotkey,
    Point,
    Color,
    Dropdown,
    ImageTemplate
}
