namespace Ajudante.Core;

public class Connection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string SourceNodeId { get; set; }
    public required string SourcePort { get; set; }
    public required string TargetNodeId { get; set; }
    public required string TargetPort { get; set; }
}
