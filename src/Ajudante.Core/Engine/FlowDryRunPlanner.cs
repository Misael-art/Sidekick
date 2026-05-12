using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public enum DryRunStepStatus
{
    Ready,
    Warning,
    Blocked
}

public sealed class DryRunNodeStep
{
    public required string NodeId { get; set; }
    public required string TypeId { get; set; }
    public string DisplayName { get; set; } = "";
    public DryRunStepStatus Status { get; set; } = DryRunStepStatus.Ready;
    public bool RequiresConfirmation { get; set; }
    public bool IsDestructive { get; set; }
    public string Message { get; set; } = "";
}

public sealed class DryRunCheckpoint
{
    public required string Kind { get; set; }
    public required string Message { get; set; }
    public string? NodeId { get; set; }
}

public sealed class FlowDryRunReport
{
    public bool CanRun { get; set; }
    public string Summary { get; set; } = "";
    public ValidationResult Validation { get; set; } = new();
    public SecurityReport Security { get; set; } = new();
    public FlowHealthReport Health { get; set; } = new();
    public List<DryRunNodeStep> Steps { get; set; } = [];
    public List<DryRunCheckpoint> Checkpoints { get; set; } = [];
}

/// <summary>
/// Builds a local, non-executing preview of a flow run. It never calls node ExecuteAsync.
/// </summary>
public sealed class FlowDryRunPlanner
{
    private readonly INodeRegistry _registry;

    public FlowDryRunPlanner(INodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public FlowDryRunReport CreateReport(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var validation = new FlowValidator(_registry).Validate(flow);
        var security = new FlowSecurityAnalyzer().Analyze(flow);
        var health = new FlowExperienceAnalyzer(_registry).Analyze(flow);
        var orderedNodes = OrderNodesForPreview(flow);
        var checkpoints = BuildCheckpoints(validation, security);
        var steps = orderedNodes.Select(node => BuildStep(node, validation, security)).ToList();

        return new FlowDryRunReport
        {
            CanRun = validation.IsValid && security.IsSafeToRun,
            Summary = BuildSummary(validation, security, steps),
            Validation = validation,
            Security = security,
            Health = health,
            Steps = steps,
            Checkpoints = checkpoints
        };
    }

    private DryRunNodeStep BuildStep(NodeInstance node, ValidationResult validation, SecurityReport security)
    {
        var definition = _registry.GetDefinition(node.TypeId);
        var nodeValidationIssues = validation.Issues
            .Where(issue => string.Equals(issue.NodeId, node.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var nodeSecurityIssues = security.Issues
            .Where(issue => string.Equals(issue.NodeId, node.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var isDestructive = nodeSecurityIssues.Any(issue => issue.Severity == SecuritySeverity.Block) || IsDestructiveType(node.TypeId);
        var blocked = nodeValidationIssues.Any(issue => issue.Severity == ValidationSeverity.Error)
            || nodeSecurityIssues.Any(issue => issue.Severity == SecuritySeverity.Block);
        var warning = nodeValidationIssues.Any(issue => issue.Severity == ValidationSeverity.Warning)
            || nodeSecurityIssues.Any(issue => issue.Severity == SecuritySeverity.Warning);

        return new DryRunNodeStep
        {
            NodeId = node.Id,
            TypeId = node.TypeId,
            DisplayName = definition?.DisplayName ?? node.TypeId,
            Status = blocked ? DryRunStepStatus.Blocked : warning ? DryRunStepStatus.Warning : DryRunStepStatus.Ready,
            RequiresConfirmation = isDestructive,
            IsDestructive = isDestructive,
            Message = ResolveStepMessage(node, nodeValidationIssues, nodeSecurityIssues, isDestructive)
        };
    }

    private static List<DryRunCheckpoint> BuildCheckpoints(ValidationResult validation, SecurityReport security)
    {
        var checkpoints = new List<DryRunCheckpoint>();

        foreach (var issue in validation.Issues.Where(issue => issue.Severity == ValidationSeverity.Error))
        {
            checkpoints.Add(new DryRunCheckpoint
            {
                Kind = "validation-error",
                NodeId = issue.NodeId,
                Message = issue.Message
            });
        }

        foreach (var issue in security.Issues.Where(issue => issue.Severity == SecuritySeverity.Block))
        {
            checkpoints.Add(new DryRunCheckpoint
            {
                Kind = "destructive-action",
                NodeId = issue.NodeId,
                Message = issue.Message
            });
        }

        return checkpoints;
    }

    private static string ResolveStepMessage(
        NodeInstance node,
        IReadOnlyList<ValidationIssue> validationIssues,
        IReadOnlyList<SecurityIssue> securityIssues,
        bool isDestructive)
    {
        var firstBlockingValidation = validationIssues.FirstOrDefault(issue => issue.Severity == ValidationSeverity.Error);
        if (firstBlockingValidation is not null)
        {
            return $"Bloqueado: {firstBlockingValidation.Message}";
        }

        var firstSecurityBlock = securityIssues.FirstOrDefault(issue => issue.Severity == SecuritySeverity.Block);
        if (firstSecurityBlock is not null)
        {
            return $"Pausa obrigatoria: {firstSecurityBlock.Message}";
        }

        if (isDestructive)
        {
            return "Pausa obrigatoria antes desta acao sensivel.";
        }

        var warning = validationIssues.FirstOrDefault(issue => issue.Severity == ValidationSeverity.Warning)?.Message
            ?? securityIssues.FirstOrDefault(issue => issue.Severity == SecuritySeverity.Warning)?.Message;
        if (warning is not null)
        {
            return $"Aviso: {warning}";
        }

        var macroMessage = ResolveMacroStepMessage(node);
        return macroMessage ?? $"Pronto para simular '{node.TypeId}'.";
    }

    private static string? ResolveMacroStepMessage(NodeInstance node)
    {
        var target = FirstNonEmpty(
            GetStringValue(node, "elementName"),
            GetStringValue(node, "automationId"),
            GetStringValue(node, "windowTitle"),
            GetStringValue(node, "processName"),
            "alvo");

        return node.TypeId switch
        {
            "action.desktopWaitElement" => $"Aguardar janela ou elemento '{target}' aparecer.",
            "action.desktopClickElement" => $"Clicar no elemento '{target}' usando seletor Mira antes de fallback.",
            "action.desktopReadElementText" => $"Ler texto do elemento '{target}' e salvar saida.",
            "action.browserWaitElement" => $"Aguardar elemento do navegador '{target}' aparecer no escopo capturado pela Mira.",
            "action.browserClick" => $"Clicar no elemento do navegador '{target}' usando seletor/fallback Mira.",
            "action.browserExtractText" => $"Ler texto do navegador em '{target}' para validar estado antes de seguir.",
            "action.browserType" => $"Digitar no elemento do navegador '{target}' apos localizar o alvo.",
            "action.mouseClick" => $"Clicar coordenada absoluta ({GetStringValue(node, "x")}, {GetStringValue(node, "y")}); revisar resiliencia.",
            "action.mouseDrag" => $"Arrastar por coordenadas de ({GetStringValue(node, "fromX")}, {GetStringValue(node, "fromY")}) ate ({GetStringValue(node, "toX")}, {GetStringValue(node, "toY")}).",
            "action.keyboardType" => string.IsNullOrWhiteSpace(GetStringValue(node, "text"))
                ? "Digitar texto redigido ou pendente de revisao; preencher antes de executar."
                : "Digitar texto configurado neste node.",
            "action.keyboardPress" => $"Pressionar tecla {FirstNonEmpty(GetStringValue(node, "key"), "configurada")}.",
            "logic.delay" => $"Pausar por {FirstNonEmpty(GetStringValue(node, "milliseconds"), "1000")} ms.",
            _ => null
        };
    }

    private static string BuildSummary(
        ValidationResult validation,
        SecurityReport security,
        IReadOnlyCollection<DryRunNodeStep> steps)
    {
        if (!validation.IsValid)
        {
            return $"Dry-run encontrou {validation.Errors.Count} erro(s) antes da execucao.";
        }

        if (!security.IsSafeToRun)
        {
            return "Dry-run exige revisao: existem acoes destrutivas ou bloqueadas por seguranca.";
        }

        var confirmations = steps.Count(step => step.RequiresConfirmation);
        return confirmations > 0
            ? $"Dry-run pronto com {confirmations} pausa(s) de confirmacao."
            : "Dry-run pronto: nenhuma acao foi executada.";
    }

    private List<NodeInstance> OrderNodesForPreview(Flow flow)
    {
        var nodesById = flow.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var outgoing = flow.Connections
            .GroupBy(connection => connection.SourceNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var incomingTargets = flow.Connections
            .Select(connection => connection.TargetNodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var startNodes = flow.Nodes
            .Where(node => _registry.GetDefinition(node.TypeId)?.Category == NodeCategory.Trigger)
            .Concat(flow.Nodes.Where(node => !incomingTargets.Contains(node.Id)))
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (startNodes.Count == 0)
        {
            startNodes = flow.Nodes.Take(1).ToList();
        }

        var ordered = new List<NodeInstance>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<NodeInstance>(startNodes);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node.Id))
            {
                continue;
            }

            ordered.Add(node);
            if (!outgoing.TryGetValue(node.Id, out var nextConnections))
            {
                continue;
            }

            foreach (var connection in nextConnections.OrderBy(connection => connection.SourcePort, StringComparer.OrdinalIgnoreCase))
            {
                if (nodesById.TryGetValue(connection.TargetNodeId, out var target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        foreach (var node in flow.Nodes)
        {
            if (visited.Add(node.Id))
            {
                ordered.Add(node);
            }
        }

        return ordered;
    }

    private static bool IsDestructiveType(string typeId)
    {
        return typeId.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || typeId.Contains("killProcess", StringComparison.OrdinalIgnoreCase)
            || typeId.Contains("systemPower", StringComparison.OrdinalIgnoreCase)
            || typeId.Contains("install", StringComparison.OrdinalIgnoreCase)
            || typeId.Contains("hardwareDevice", StringComparison.OrdinalIgnoreCase)
            || typeId.Contains("displaySettings", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStringValue(NodeInstance node, string propertyId)
    {
        if (!node.Properties.TryGetValue(propertyId, out var value) || value is null)
        {
            return "";
        }

        return value switch
        {
            string text => text.Trim(),
            System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String => element.GetString()?.Trim() ?? "",
            System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number => element.GetRawText(),
            _ => value.ToString()?.Trim() ?? ""
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }
}
