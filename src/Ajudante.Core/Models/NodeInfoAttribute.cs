namespace Ajudante.Core;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class NodeInfoAttribute : Attribute
{
    public required new string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public required NodeCategory Category { get; init; }
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#888888";
}
