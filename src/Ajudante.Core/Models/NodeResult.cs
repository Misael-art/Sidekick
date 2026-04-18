namespace Ajudante.Core;

public class NodeResult
{
    public bool Success { get; init; }
    public string? OutputPort { get; init; }
    public Dictionary<string, object?> Outputs { get; init; } = new();
    public string? Error { get; init; }

    public static NodeResult Ok(string? outputPort = null) => new()
    {
        Success = true,
        OutputPort = outputPort
    };

    public static NodeResult Ok(string outputPort, Dictionary<string, object?> outputs) => new()
    {
        Success = true,
        OutputPort = outputPort,
        Outputs = outputs
    };

    public static NodeResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
