namespace Ajudante.Core.Interfaces;

public interface ITriggerNode : INode
{
    event Action<TriggerEventArgs>? Triggered;
    Task StartWatchingAsync(CancellationToken ct);
    Task StopWatchingAsync();
}

public class TriggerEventArgs : EventArgs
{
    public Dictionary<string, object?> Data { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
