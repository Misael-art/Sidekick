using Ajudante.Core.Engine;

namespace Ajudante.Core;

public sealed class RunnableFlowQuery
{
    public IReadOnlyList<string> AllowedFlowIds { get; init; } = Array.Empty<string>();
    public bool IncludeHighRisk { get; init; }
    public string? CurrentFlowId { get; init; }
}

public sealed class RunnableFlowSummary
{
    public string FlowId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string RiskLevel { get; init; } = "low";
    public bool IsPortfolio { get; init; }
    public bool RequiresLocalConfirmation { get; init; }
}

public sealed class FlowInvocationRequest
{
    public string FlowId { get; init; } = "";
    public string Source { get; init; } = "local";
    public string RequestedBy { get; init; } = "";
    public bool AllowHighRisk { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("n");
    public string? CurrentFlowId { get; init; }
    public IReadOnlyList<string> AllowedFlowIds { get; init; } = Array.Empty<string>();
}

public sealed class FlowInvocationResult
{
    public FlowInvocationStatus Status { get; init; }
    public string FlowId { get; init; } = "";
    public string FlowName { get; init; } = "";
    public string Message { get; init; } = "";
    public ValidationResult? Validation { get; init; }
    public SecurityReport? Security { get; init; }
}

public enum FlowInvocationStatus
{
    Queued,
    Blocked,
    NeedsConfiguration,
    RequiresLocalConfirmation,
    NotFound,
    Invalid,
    Unavailable
}
