namespace Ajudante.Core.Interfaces;

public interface INode
{
    string Id { get; set; }
    NodeDefinition Definition { get; }
    Task<NodeResult> ExecuteAsync(Engine.FlowExecutionContext context, CancellationToken ct);
    void Configure(Dictionary<string, object?> properties);
}
