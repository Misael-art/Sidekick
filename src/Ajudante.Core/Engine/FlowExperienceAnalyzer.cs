using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Engine;

public enum FlowHealthSeverity
{
    Info,
    Warning,
    Error
}

public sealed class FlowHealthIssue
{
    public required string Code { get; set; }
    public required FlowHealthSeverity Severity { get; set; }
    public required string Message { get; set; }
    public string? NodeId { get; set; }
    public string? PropertyId { get; set; }
    public string? Action { get; set; }
}

public sealed class FlowHealthSuggestion
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Detail { get; set; }
    public string Action { get; set; } = "";
    public string Priority { get; set; } = "medium";
    public string? NodeId { get; set; }
}

public sealed class FlowHealthReport
{
    public int Score { get; set; }
    public string Level { get; set; } = "good";
    public bool CanRunWithoutAttention { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<FlowHealthIssue> Issues { get; set; } = [];
    public List<FlowHealthSuggestion> Suggestions { get; set; } = [];
}

/// <summary>
/// Product-facing analyzer for the "Flow Health" panel. It composes validation and security
/// checks into user-oriented issues without executing any node.
/// </summary>
public sealed class FlowExperienceAnalyzer
{
    private readonly INodeRegistry _registry;

    public FlowExperienceAnalyzer(INodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public FlowHealthReport Analyze(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var validator = new FlowValidator(_registry);
        var validation = validator.Validate(flow);
        var security = new FlowSecurityAnalyzer().Analyze(flow);
        var issues = new List<FlowHealthIssue>();
        var suggestions = new List<FlowHealthSuggestion>();

        foreach (var issue in validation.Issues)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = issue.Code,
                Severity = issue.Severity == ValidationSeverity.Error ? FlowHealthSeverity.Error : FlowHealthSeverity.Warning,
                Message = ToUserMessage(issue.Message),
                NodeId = issue.NodeId,
                PropertyId = issue.PropertyId,
                Action = ResolveActionForValidation(issue.Code)
            });
        }

        foreach (var issue in security.Issues)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = issue.Code,
                Severity = issue.Severity switch
                {
                    SecuritySeverity.Block => FlowHealthSeverity.Error,
                    SecuritySeverity.Warning => FlowHealthSeverity.Warning,
                    _ => FlowHealthSeverity.Info
                },
                Message = ToUserMessage(issue.Message),
                NodeId = issue.NodeId,
                Action = issue.Severity == SecuritySeverity.Block ? "dryRun.review" : "safety.review"
            });
        }

        AddContextualIssues(flow, issues);
        AddSuggestions(flow, issues, suggestions);

        var distinctIssues = issues
            .GroupBy(issue => $"{issue.Code}:{issue.NodeId}:{issue.PropertyId}:{issue.Message}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.NodeId)
            .ThenBy(issue => issue.Code)
            .ToList();

        var score = ComputeScore(distinctIssues);
        return new FlowHealthReport
        {
            Score = score,
            Level = score >= 85 ? "otimo" : score >= 70 ? "bom" : score >= 45 ? "atencao" : "critico",
            CanRunWithoutAttention = validation.IsValid && security.IsSafeToRun,
            GeneratedAt = DateTime.UtcNow,
            Issues = distinctIssues,
            Suggestions = suggestions
                .GroupBy(suggestion => $"{suggestion.Id}:{suggestion.NodeId}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(suggestion => suggestion.Priority == "high" ? 0 : suggestion.Priority == "medium" ? 1 : 2)
                .ThenBy(suggestion => suggestion.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private void AddContextualIssues(Flow flow, ICollection<FlowHealthIssue> issues)
    {
        if (flow.Nodes.Count == 0)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = "flow.empty",
                Severity = FlowHealthSeverity.Error,
                Message = "O canvas ainda nao tem passos. Comece por uma receita, captura Mira/Snip ou inicio manual.",
                Action = "guided.start"
            });
            return;
        }

        if (flow.Nodes.All(node => !IsContinuousTrigger(node.TypeId)))
        {
            issues.Add(new FlowHealthIssue
            {
                Code = "flow.monitoring.manualOnly",
                Severity = FlowHealthSeverity.Info,
                Message = "Este flow tem apenas inicio manual. Para monitorar em segundo plano, adicione um gatilho continuo.",
                Action = "guided.trigger"
            });
        }

        foreach (var node in flow.Nodes)
        {
            var definition = _registry.GetDefinition(node.TypeId);
            if (definition is null || !HasSelectorProperties(definition))
            {
                continue;
            }

            var automationId = GetStringValue(node, "automationId");
            var elementName = GetStringValue(node, "elementName");
            var name = GetStringValue(node, "name");
            var controlType = GetStringValue(node, "controlType");
            var processName = GetStringValue(node, "processName");
            var processPath = GetStringValue(node, "processPath");
            var windowTitle = GetStringValue(node, "windowTitle");

            if (!string.IsNullOrWhiteSpace(automationId) &&
                (!string.IsNullOrWhiteSpace(processName) || !string.IsNullOrWhiteSpace(processPath) || !string.IsNullOrWhiteSpace(windowTitle)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(automationId) &&
                (!string.IsNullOrWhiteSpace(elementName) || !string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(controlType)))
            {
                issues.Add(new FlowHealthIssue
                {
                    Code = "selector.weak",
                    Severity = FlowHealthSeverity.Warning,
                    Message = $"Node '{node.Id}' usa seletor Mira fraco. Prefira AutomationId + processo/janela e mantenha fallback relativo.",
                    NodeId = node.Id,
                    Action = "selector.doctor"
                });
            }
        }

        AddMacroRecorderIssues(flow, issues);
    }

    private static void AddSuggestions(
        Flow flow,
        IReadOnlyCollection<FlowHealthIssue> issues,
        ICollection<FlowHealthSuggestion> suggestions)
    {
        if (flow.Nodes.Count == 0)
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "guided.recipe",
                Title = "Comece por uma receita guiada",
                Detail = "Use uma receita local, capture um elemento com Mira ou grave um rascunho antes de montar nodes manualmente.",
                Action = "guided.recipe",
                Priority = "high"
            });
            return;
        }

        if (issues.Any(issue => issue.Code == "property.template.unresolved"))
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "flow.variables",
                Title = "Declare ou corrija variaveis",
                Detail = "Revise os campos com {{variavel}} e cadastre defaults para evitar falhas no runtime.",
                Action = "flow.variables",
                Priority = "high"
            });
        }

        if (issues.Any(issue => issue.Code.StartsWith("selector.", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "selector.doctor",
                Title = "Rodar Selector Doctor",
                Detail = "Teste o seletor agora, veja a forca e repare usando a ultima captura Mira quando necessario.",
                Action = "selector.doctor",
                Priority = "high"
            });
        }

        if (issues.Any(issue => issue.Code.StartsWith("macro.", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "macro.review",
                Title = "Revisar passos gravados",
                Detail = "Reduza coordenadas absolutas, adicione escopo de janela/processo e repare seletores fracos antes de armar.",
                Action = "macro.review",
                Priority = "high"
            });
        }

        if (issues.Any(issue => issue.Code is "security.destructiveAction" or "security.globalInput"))
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "dryRun.review",
                Title = "Fazer dry-run antes de executar",
                Detail = "Revise a ordem dos passos e confirme acoes destrutivas antes do runtime real.",
                Action = "dryRun.review",
                Priority = "high"
            });
        }

        if (issues.Any(issue => issue.Code == "flow.monitoring.manualOnly"))
        {
            suggestions.Add(new FlowHealthSuggestion
            {
                Id = "guided.trigger",
                Title = "Escolher gatilho de monitoramento",
                Detail = "Se a automacao deve reagir sozinha, troque inicio manual por janela, processo, horario, imagem ou arquivo.",
                Action = "guided.trigger",
                Priority = "low"
            });
        }
    }

    private static int ComputeScore(IEnumerable<FlowHealthIssue> issues)
    {
        var score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                FlowHealthSeverity.Error => 24,
                FlowHealthSeverity.Warning => 10,
                _ => 3
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    private static string? ResolveActionForValidation(string code)
    {
        if (code.StartsWith("selector.", StringComparison.OrdinalIgnoreCase))
        {
            return "selector.doctor";
        }

        if (code.Contains("template", StringComparison.OrdinalIgnoreCase))
        {
            return "flow.variables";
        }

        if (code.StartsWith("property.", StringComparison.OrdinalIgnoreCase))
        {
            return "property.fix";
        }

        return code.StartsWith("flow.", StringComparison.OrdinalIgnoreCase) ? "guided.start" : null;
    }

    private static bool HasSelectorProperties(NodeDefinition definition)
    {
        return definition.Properties.Any(property =>
            string.Equals(property.Id, "windowTitle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "automationId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "elementName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "name", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Id, "controlType", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsContinuousTrigger(string typeId)
    {
        return typeId.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(typeId, "trigger.manualStart", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddMacroRecorderIssues(Flow flow, ICollection<FlowHealthIssue> issues)
    {
        var absoluteCoordinateNodes = flow.Nodes
            .Where(IsAbsoluteCoordinateNode)
            .ToList();
        if (absoluteCoordinateNodes.Count >= 3)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = "macro.absoluteCoordinates",
                Severity = FlowHealthSeverity.Warning,
                Message = $"Macro com {absoluteCoordinateNodes.Count} passos por coordenada absoluta. Isso tende a quebrar com DPI, resolucao ou janela reposicionada.",
                Action = "macro.review"
            });
        }

        foreach (var node in flow.Nodes.Where(IsScopedDesktopNode))
        {
            var hasScope = !string.IsNullOrWhiteSpace(GetStringValue(node, "windowTitle"))
                || !string.IsNullOrWhiteSpace(GetStringValue(node, "processName"))
                || !string.IsNullOrWhiteSpace(GetStringValue(node, "processPath"));
            if (!hasScope)
            {
                issues.Add(new FlowHealthIssue
                {
                    Code = "macro.missingWindowScope",
                    Severity = FlowHealthSeverity.Warning,
                    Message = $"Node '{node.Id}' usa seletor desktop sem escopo de janela ou processo.",
                    NodeId = node.Id,
                    Action = "selector.doctor"
                });
            }
        }

        foreach (var node in flow.Nodes.Where(node => string.Equals(node.TypeId, "action.keyboardType", StringComparison.OrdinalIgnoreCase)))
        {
            var text = GetStringValue(node, "text");
            if (LooksSensitive(text))
            {
                issues.Add(new FlowHealthIssue
                {
                    Code = "macro.sensitiveTextCaptured",
                    Severity = FlowHealthSeverity.Error,
                    Message = $"Node '{node.Id}' parece conter senha, token ou API key gravada em texto bruto.",
                    NodeId = node.Id,
                    PropertyId = "text",
                    Action = "secrets.redact"
                });
            }
        }

        var implicitWaits = flow.Nodes.Count(node => string.Equals(node.TypeId, "logic.delay", StringComparison.OrdinalIgnoreCase));
        if (implicitWaits >= 4)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = "macro.tooManyImplicitWaits",
                Severity = FlowHealthSeverity.Warning,
                Message = $"Macro usa {implicitWaits} pausas fixas. Prefira aguardar janela/elemento para reduzir falhas por timing.",
                Action = "macro.review"
            });
        }

        var redundantSteps = CountRedundantCoordinateSteps(flow.Nodes);
        if (redundantSteps > 0)
        {
            issues.Add(new FlowHealthIssue
            {
                Code = "macro.redundantSteps",
                Severity = FlowHealthSeverity.Info,
                Message = "Ha passos gravados muito proximos entre si. Remova eventos redundantes no painel de revisao.",
                Action = "macro.review"
            });
        }
    }

    private static bool IsAbsoluteCoordinateNode(NodeInstance node)
    {
        return string.Equals(node.TypeId, "action.mouseClick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.TypeId, "action.mouseMove", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.TypeId, "action.mouseDrag", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScopedDesktopNode(NodeInstance node)
    {
        return node.TypeId.Contains("desktop", StringComparison.OrdinalIgnoreCase)
            || node.TypeId.Contains("window", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        return normalized.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("password=", StringComparison.Ordinal)
            || normalized.Contains("senha=", StringComparison.Ordinal)
            || normalized.Contains("token=", StringComparison.Ordinal)
            || normalized.Contains("api_key", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal);
    }

    private static int CountRedundantCoordinateSteps(IReadOnlyList<NodeInstance> nodes)
    {
        var count = 0;
        for (var i = 1; i < nodes.Count; i++)
        {
            var previous = nodes[i - 1];
            var current = nodes[i];
            if (!string.Equals(previous.TypeId, current.TypeId, StringComparison.OrdinalIgnoreCase) ||
                !IsAbsoluteCoordinateNode(current))
            {
                continue;
            }

            var previousX = GetIntValue(previous, "x", GetIntValue(previous, "toX"));
            var previousY = GetIntValue(previous, "y", GetIntValue(previous, "toY"));
            var currentX = GetIntValue(current, "x", GetIntValue(current, "toX"));
            var currentY = GetIntValue(current, "y", GetIntValue(current, "toY"));
            if (Math.Abs(previousX - currentX) <= 4 && Math.Abs(previousY - currentY) <= 4)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetIntValue(NodeInstance node, string propertyId, int fallback = 0)
    {
        if (!node.Properties.TryGetValue(propertyId, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            int number => number,
            long number => (int)number,
            double number => (int)number,
            float number => (int)number,
            string text when int.TryParse(text, out var parsed) => parsed,
            System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt32(out var parsed) => parsed,
            _ => fallback
        };
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
            _ => value.ToString()?.Trim() ?? ""
        };
    }

    private static string ToUserMessage(string message)
    {
        return message
            .Replace("Flow has no nodes.", "O flow nao tem nodes.", StringComparison.Ordinal)
            .Replace("Flow has no trigger node. It can only be started manually.", "O flow nao tem gatilho; ele so inicia manualmente.", StringComparison.Ordinal)
            .Replace("is missing required property", "esta sem a propriedade obrigatoria", StringComparison.Ordinal)
            .Replace("has unresolved reference", "tem referencia nao resolvida", StringComparison.Ordinal);
    }
}
