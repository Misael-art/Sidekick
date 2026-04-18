namespace Ajudante.Core;

public class NodeInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string TypeId { get; set; }
    public NodePosition Position { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}
