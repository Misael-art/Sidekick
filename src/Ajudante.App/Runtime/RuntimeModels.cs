using System.Text.Json.Serialization;
using Ajudante.Core;

namespace Ajudante.App.Runtime;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowRuntimeState
{
    Inactive,
    Armed,
    Queued,
    Running,
    Error
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowRunSource
{
    Manual,
    Trigger
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopFlowMode
{
    CurrentOnly,
    CancelAll
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowExecutionResult
{
    Running,
    Completed,
    Error,
    Cancelled
}

public sealed class CurrentRunSnapshot
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public FlowRunSource Source { get; init; }
    public string? TriggerNodeId { get; init; }
    public DateTime StartedAt { get; init; }
}

public sealed class FlowRuntimeSnapshot
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public FlowRuntimeState State { get; init; }
    public bool IsArmed { get; init; }
    public bool IsRunning { get; init; }
    public bool QueuePending { get; init; }
    public string[] ActiveTriggerNodeIds { get; init; } = [];
    public DateTime? LastTriggerAt { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastError { get; init; }
}

public sealed class RuntimeStatusSnapshot
{
    public bool IsRunning { get; init; }
    public int QueueLength { get; init; }
    public int ArmedFlowCount { get; init; }
    public CurrentRunSnapshot? CurrentRun { get; init; }
    public FlowRuntimeSnapshot[] Flows { get; init; } = [];
}

public sealed class FlowQueuedEvent
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public FlowRunSource Source { get; init; }
    public string? TriggerNodeId { get; init; }
    public int QueueLength { get; init; }
    public bool QueuePending { get; init; }
}

public sealed class TriggerRuntimeEvent
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public string TriggerNodeId { get; init; } = "";
    public DateTime TriggeredAt { get; init; }
}

public sealed class RuntimeErrorEvent
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public string Error { get; init; } = "";
}

public sealed class RuntimePhaseEvent
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public string NodeId { get; init; } = "";
    public string Phase { get; init; } = "";
    public string? Message { get; init; }
    public object? Detail { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class FlowExecutionEvent
{
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public FlowRunSource Source { get; init; }
    public string? TriggerNodeId { get; init; }
}

public sealed class ExecutionHistoryLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "info";
    public string? NodeId { get; init; }
    public string Message { get; init; } = "";
}

public sealed class ExecutionNodeStatusEntry
{
    public DateTime Timestamp { get; init; }
    public string NodeId { get; init; } = "";
    public NodeStatus Status { get; init; }
}

public sealed class FlowExecutionHistoryEntry
{
    public string RunId { get; init; } = "";
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public FlowRunSource Source { get; init; }
    public string? TriggerNodeId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public FlowExecutionResult Result { get; init; }
    public string? Error { get; init; }
    public ExecutionHistoryLogEntry[] Logs { get; init; } = [];
    public ExecutionNodeStatusEntry[] NodeStatuses { get; init; } = [];
}

public sealed class FlowActivationResult
{
    public bool Armed { get; init; }
    public FlowRuntimeSnapshot Snapshot { get; init; } = new();
    public string[] Warnings { get; init; } = [];
}

public sealed class StopFlowResult
{
    public bool CancelledCurrentRun { get; init; }
    public int ClearedQueuedRuns { get; init; }
    public int RemainingQueueLength { get; init; }
    public bool IsRunning { get; init; }
}
