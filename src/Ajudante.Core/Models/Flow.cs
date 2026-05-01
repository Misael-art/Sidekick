using System.Text.Json.Serialization;

namespace Ajudante.Core;

public class Flow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Flow";
    public int Version { get; set; } = 1;
    public List<FlowVariable> Variables { get; set; } = new();
    public List<NodeInstance> Nodes { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<StickyNote> Annotations { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

public class StickyNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Color { get; set; } = "yellow";
    public NodePosition Position { get; set; } = new();
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 160;
}

public class FlowVariable
{
    public required string Name { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VariableType Type { get; set; } = VariableType.String;

    public object? Default { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VariableType
{
    String,
    Integer,
    Float,
    Boolean
}
