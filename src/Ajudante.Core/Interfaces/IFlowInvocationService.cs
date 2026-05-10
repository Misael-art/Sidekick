using Ajudante.Core;

namespace Ajudante.Core.Interfaces;

public interface IFlowInvocationService
{
    Task<IReadOnlyList<RunnableFlowSummary>> ListRunnableFlowsAsync(
        RunnableFlowQuery query,
        CancellationToken cancellationToken = default);

    Task<FlowInvocationResult> QueueFlowAsync(
        FlowInvocationRequest request,
        CancellationToken cancellationToken = default);
}
