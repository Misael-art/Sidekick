namespace Ajudante.Core;

public class Variable
{
    public required string Name { get; set; }
    public VariableType Type { get; set; } = VariableType.String;
    public object? Value { get; set; }
}
