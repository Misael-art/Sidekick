namespace Ajudante.Core;

public class Port
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public PortDirection Direction { get; init; }
    public PortDataType DataType { get; init; } = PortDataType.Flow;
    public object? Value { get; set; }
}

public enum PortDirection
{
    Input,
    Output
}
