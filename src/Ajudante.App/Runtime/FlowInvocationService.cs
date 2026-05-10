using System.Text.RegularExpressions;
using System.IO;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Serialization;

namespace Ajudante.App.Runtime;

public sealed class FlowInvocationService : IFlowInvocationService
{
    private readonly INodeRegistry _registry;
    private readonly FlowRuntimeManager _runtimeManager;
    private readonly string _flowsDirectory;
    private readonly string _seedFlowsDirectory;

    public FlowInvocationService(
        INodeRegistry registry,
        FlowRuntimeManager runtimeManager,
        string flowsDirectory,
        string seedFlowsDirectory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        _flowsDirectory = flowsDirectory ?? throw new ArgumentNullException(nameof(flowsDirectory));
        _seedFlowsDirectory = seedFlowsDirectory ?? throw new ArgumentNullException(nameof(seedFlowsDirectory));
    }

    public async Task<IReadOnlyList<RunnableFlowSummary>> ListRunnableFlowsAsync(
        RunnableFlowQuery query,
        CancellationToken cancellationToken = default)
    {
        var patterns = NormalizeAllowlist(query.AllowedFlowIds);
        var flows = await LoadCandidateFlowsAsync(cancellationToken);
        var summaries = new List<RunnableFlowSummary>();

        foreach (var flow in flows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(flow.Id) ||
                IsSelfInvocation(flow.Id, query.CurrentFlowId) ||
                !MatchesAllowlist(flow.Id, patterns))
            {
                continue;
            }

            var validation = Validate(flow);
            if (!validation.IsValid)
            {
                continue;
            }

            var security = AnalyzeSecurity(flow);
            var requiresConfirmation = !security.IsSafeToRun ||
                string.Equals(security.RiskLevel, "high", StringComparison.OrdinalIgnoreCase);

            if (requiresConfirmation && !query.IncludeHighRisk)
            {
                continue;
            }

            summaries.Add(new RunnableFlowSummary
            {
                FlowId = flow.Id,
                Name = string.IsNullOrWhiteSpace(flow.Name) ? flow.Id : flow.Name,
                Category = ResolveCategory(flow),
                RiskLevel = security.RiskLevel,
                IsPortfolio = IsPortfolio(flow),
                RequiresLocalConfirmation = requiresConfirmation
            });
        }

        return summaries
            .OrderByDescending(summary => summary.IsPortfolio)
            .ThenBy(summary => summary.RequiresLocalConfirmation)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<FlowInvocationResult> QueueFlowAsync(
        FlowInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FlowId))
        {
            return new FlowInvocationResult
            {
                Status = FlowInvocationStatus.NotFound,
                Message = "Flow nao informado."
            };
        }

        var patterns = NormalizeAllowlist(request.AllowedFlowIds);
        if (!MatchesAllowlist(request.FlowId, patterns))
        {
            return new FlowInvocationResult
            {
                Status = FlowInvocationStatus.Blocked,
                FlowId = request.FlowId,
                Message = "Flow bloqueado pela allowlist local."
            };
        }

        if (IsSelfInvocation(request.FlowId, request.CurrentFlowId))
        {
            return new FlowInvocationResult
            {
                Status = FlowInvocationStatus.Blocked,
                FlowId = request.FlowId,
                Message = "Auto-recursao bloqueada: um flow nao pode enfileirar a si mesmo."
            };
        }

        var flow = await FindFlowAsync(request.FlowId, cancellationToken);
        if (flow == null)
        {
            return new FlowInvocationResult
            {
                Status = FlowInvocationStatus.NotFound,
                FlowId = request.FlowId,
                Message = "Flow nao encontrado."
            };
        }

        var validation = Validate(flow);
        var security = AnalyzeSecurity(flow);
        if (!validation.IsValid)
        {
            return new FlowInvocationResult
            {
                Status = HasConfigurationErrors(validation) ? FlowInvocationStatus.NeedsConfiguration : FlowInvocationStatus.Invalid,
                FlowId = flow.Id,
                FlowName = flow.Name,
                Message = $"Flow precisa de ajuste antes de executar: {string.Join(" | ", validation.Errors.Take(3))}",
                Validation = validation,
                Security = security
            };
        }

        var isHighRisk = !security.IsSafeToRun ||
            string.Equals(security.RiskLevel, "high", StringComparison.OrdinalIgnoreCase);
        if (isHighRisk && !request.AllowHighRisk)
        {
            return new FlowInvocationResult
            {
                Status = FlowInvocationStatus.RequiresLocalConfirmation,
                FlowId = flow.Id,
                FlowName = flow.Name,
                Message = "Flow requer confirmacao local no Sidekick antes de executar.",
                Validation = validation,
                Security = security
            };
        }

        var queueEvent = _runtimeManager.QueueManualRun(flow);
        return new FlowInvocationResult
        {
            Status = FlowInvocationStatus.Queued,
            FlowId = flow.Id,
            FlowName = flow.Name,
            Message = $"Flow '{flow.Name}' enfileirado. Posicao aproximada da fila: {Math.Max(1, queueEvent.QueueLength)}.",
            Validation = validation,
            Security = security
        };
    }

    private async Task<Flow?> FindFlowAsync(string flowId, CancellationToken cancellationToken)
    {
        var flows = await LoadCandidateFlowsAsync(cancellationToken);
        return flows.FirstOrDefault(flow => string.Equals(flow.Id, flowId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<Flow>> LoadCandidateFlowsAsync(CancellationToken cancellationToken)
    {
        var flows = new List<Flow>();
        flows.AddRange(await SafeLoadAllAsync(_flowsDirectory));
        flows.AddRange(await SafeLoadAllAsync(_seedFlowsDirectory));

        var seedById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .Where(flow => IsUnderSeedDirectory(flow))
            .GroupBy(flow => flow.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var flow in flows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(flow.Id) && seedById.TryGetValue(flow.Id, out var seed))
            {
                MergeMissingVariables(flow, seed);
            }
        }

        return flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .GroupBy(flow => flow.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(flow => IsUnderSeedDirectory(flow) ? 0 : 1)
                .ThenByDescending(flow => flow.ModifiedAt)
                .First())
            .ToArray();
    }

    private async Task<List<Flow>> SafeLoadAllAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var flows = await FlowSerializer.LoadAllAsync(directory);
        foreach (var flow in flows)
        {
            flow.Annotations.Add(new StickyNote
            {
                Id = "__sidekick-source",
                Title = "source",
                Body = Path.GetFullPath(directory),
                ContentFormat = "internal"
            });
        }

        return flows;
    }

    private bool IsUnderSeedDirectory(Flow flow)
    {
        var marker = flow.Annotations.FirstOrDefault(note => note.Id == "__sidekick-source");
        return marker != null &&
               string.Equals(Path.GetFullPath(marker.Body), Path.GetFullPath(_seedFlowsDirectory), StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeMissingVariables(Flow flow, Flow seed)
    {
        var existing = flow.Variables
            .Select(variable => variable.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in seed.Variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Name) && existing.Add(variable.Name))
            {
                flow.Variables.Add(new FlowVariable
                {
                    Name = variable.Name,
                    Type = variable.Type,
                    Default = variable.Default
                });
            }
        }
    }

    private ValidationResult Validate(Flow flow)
    {
        return new FlowValidator(_registry).Validate(flow);
    }

    private static SecurityReport AnalyzeSecurity(Flow flow)
    {
        return new FlowSecurityAnalyzer().Analyze(flow);
    }

    private static bool HasConfigurationErrors(ValidationResult validation)
    {
        return validation.Errors.Any(error =>
            error.Contains("required property", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("image template", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("incomplete selector", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("unresolved reference", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveCategory(Flow flow)
    {
        if (IsPortfolio(flow))
        {
            return "portfolio";
        }

        if (flow.Id.StartsWith("recipe-", StringComparison.OrdinalIgnoreCase))
        {
            return "recipe";
        }

        return "user";
    }

    private static bool IsPortfolio(Flow flow)
    {
        return flow.Id.StartsWith("portfolio-", StringComparison.OrdinalIgnoreCase) ||
               flow.Name.Contains("Portfolio", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelfInvocation(string flowId, string? currentFlowId)
    {
        return !string.IsNullOrWhiteSpace(currentFlowId) &&
               string.Equals(flowId, currentFlowId, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] NormalizeAllowlist(IReadOnlyList<string>? patterns)
    {
        var normalized = (patterns ?? Array.Empty<string>())
            .SelectMany(pattern => pattern.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToArray();

        return normalized.Length == 0 ? ["portfolio-*"] : normalized;
    }

    private static bool MatchesAllowlist(string flowId, IReadOnlyList<string> patterns)
    {
        return patterns.Any(pattern => WildcardMatches(flowId, pattern));
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        if (string.Equals(pattern, "*", StringComparison.Ordinal))
        {
            return true;
        }

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
