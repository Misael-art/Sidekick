namespace Ajudante.Core.Recorder;

public static class MacroDraftBuilder
{
    public static GuidedAutomationDraft BuildDraft(
        MacroRecordingSession session,
        IEnumerable<RecorderEvent> rawEvents,
        MacroRecorderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var stoppedAt = session.StoppedAt ?? DateTime.UtcNow;
        var effectiveOptions = options ?? new MacroRecorderOptions();
        var events = MacroRecorderEventCoalescer.Coalesce(rawEvents, effectiveOptions);
        var suggestedNodes = new List<RecorderSuggestedNode>
        {
            new()
            {
                Id = "draft-start",
                TypeId = "trigger.manualStart",
                Position = new RecorderNodePosition { X = 80, Y = 120 },
                Properties = []
            }
        };
        var warnings = new List<string>();

        var stepIndex = 0;
        foreach (var recorderEvent in events)
        {
            var node = BuildNodeForEvent(recorderEvent, ++stepIndex, warnings);
            if (node is not null)
            {
                suggestedNodes.Add(node);
            }
        }

        var connections = ConnectSequentially(suggestedNodes);
        var score = ComputeDraftScore(events, warnings, suggestedNodes);

        return new GuidedAutomationDraft
        {
            Id = $"draft_{session.SessionId}",
            SessionId = session.SessionId,
            DisplayName = string.IsNullOrWhiteSpace(session.Goal) ? "Rascunho gravado" : session.Goal,
            IsDraft = true,
            StartedAt = session.StartedAt,
            StoppedAt = stoppedAt,
            Events = events,
            SuggestedNodes = suggestedNodes,
            SuggestedConnections = connections,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Limitations =
            [
                "Modo rascunho local: nenhum passo gravado executa automaticamente.",
                "Revise a timeline, remova ruido e rode dry-run antes de executar.",
                "Textos sensiveis sao redigidos por padrao e devem ser preenchidos com variavel ou secret."
            ],
            Score = score
        };
    }

    public static GuidedAutomationDraft RebuildDraftFromEditedEvents(GuidedAutomationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var session = new MacroRecordingSession
        {
            SessionId = draft.SessionId,
            StartedAt = draft.StartedAt,
            StoppedAt = draft.StoppedAt,
            Status = "stopped",
            PrivacyMode = draft.Events.Any(e => e.Privacy.IsRedacted) ? "redactSensitive" : "default",
            Goal = draft.DisplayName
        };

        var rebuilt = BuildDraft(session, draft.Events, new MacroRecorderOptions());
        rebuilt.Id = draft.Id;
        rebuilt.DisplayName = draft.DisplayName;
        rebuilt.SavedInspectionAsset = draft.SavedInspectionAsset;
        return rebuilt;
    }

    private static RecorderSuggestedNode? BuildNodeForEvent(
        RecorderEvent recorderEvent,
        int stepIndex,
        ICollection<string> draftWarnings)
    {
        return recorderEvent.Kind switch
        {
            "mouseClick" or "mouseDoubleClick" => BuildClickNode(recorderEvent, stepIndex, draftWarnings),
            "mouseDrag" => BuildDragNode(recorderEvent, stepIndex),
            "textInput" or "redactedInput" => BuildTextNode(recorderEvent, stepIndex, draftWarnings),
            "keyPress" or "hotkey" => BuildKeyPressNode(recorderEvent, stepIndex),
            "pause" => BuildPauseNode(recorderEvent, stepIndex),
            "elementSnapshot" => BuildWaitNode(recorderEvent, stepIndex, draftWarnings),
            "windowFocus" => BuildWaitNode(recorderEvent, stepIndex, draftWarnings),
            _ => null
        };
    }

    private static RecorderSuggestedNode? BuildClickNode(
        RecorderEvent recorderEvent,
        int stepIndex,
        ICollection<string> draftWarnings)
    {
        if (HasStrongSelector(recorderEvent.Element, recorderEvent.Window))
        {
            var properties = BuildSelectorProperties(recorderEvent);
            properties["clickType"] = recorderEvent.Kind == "mouseDoubleClick" ? "double" : "single";
            return new RecorderSuggestedNode
            {
                Id = $"draft-step-{stepIndex}",
                TypeId = "action.desktopClickElement",
                Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
                Properties = properties,
                Confidence = 0.9d
            };
        }

        var mouse = recorderEvent.Mouse;
        if (mouse is null)
        {
            return null;
        }

        draftWarnings.Add("Clique gravado por coordenada absoluta. Recapture com Mira para aumentar resiliencia antes de executar.");
        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "action.mouseClick",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = mouse.X,
                ["y"] = mouse.Y,
                ["button"] = mouse.Button,
                ["clickType"] = recorderEvent.Kind == "mouseDoubleClick" ? "double" : "single"
            },
            Confidence = 0.45d,
            Warnings = ["Usa coordenada absoluta; sensivel a resolucao, DPI e posicao de janela."]
        };
    }

    private static RecorderSuggestedNode? BuildWaitNode(
        RecorderEvent recorderEvent,
        int stepIndex,
        ICollection<string> draftWarnings)
    {
        if (!HasStrongSelector(recorderEvent.Element, recorderEvent.Window))
        {
            draftWarnings.Add("Foco ou snapshot sem seletor forte foi mantido na timeline, mas nao virou node resiliente.");
            return null;
        }

        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "action.desktopWaitElement",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = BuildSelectorProperties(recorderEvent),
            Confidence = 0.9d
        };
    }

    private static RecorderSuggestedNode? BuildDragNode(RecorderEvent recorderEvent, int stepIndex)
    {
        var mouse = recorderEvent.Mouse;
        if (mouse is null)
        {
            return null;
        }

        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "action.mouseDrag",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fromX"] = mouse.StartX,
                ["fromY"] = mouse.StartY,
                ["toX"] = mouse.EndX,
                ["toY"] = mouse.EndY
            },
            Confidence = 0.55d,
            Warnings = ["Drag por coordenadas absolutas; prefira alvo Mira quando o app expuser seletor."]
        };
    }

    private static RecorderSuggestedNode? BuildTextNode(
        RecorderEvent recorderEvent,
        int stepIndex,
        ICollection<string> draftWarnings)
    {
        var isRedacted = recorderEvent.Privacy.IsRedacted || recorderEvent.Kind == "redactedInput";
        if (isRedacted)
        {
            draftWarnings.Add("Texto sensivel foi redigido. O node de digitacao entra vazio e exige revisao antes de executar.");
        }

        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "action.keyboardType",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = isRedacted ? "" : recorderEvent.Text?.Value ?? "",
                ["delayBetweenKeys"] = 0
            },
            Confidence = isRedacted ? 0.65d : 0.85d,
            Warnings = isRedacted ? ["Preencha com variavel/secret antes de executar."] : []
        };
    }

    private static RecorderSuggestedNode? BuildKeyPressNode(RecorderEvent recorderEvent, int stepIndex)
    {
        var key = recorderEvent.Keyboard?.Key ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "action.keyboardPress",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = key,
                ["modifiers"] = string.Join("+", recorderEvent.Keyboard?.Modifiers ?? [])
            },
            Confidence = 0.8d
        };
    }

    private static RecorderSuggestedNode BuildPauseNode(RecorderEvent recorderEvent, int stepIndex)
    {
        return new RecorderSuggestedNode
        {
            Id = $"draft-step-{stepIndex}",
            TypeId = "logic.delay",
            Position = new RecorderNodePosition { X = 360 + ((stepIndex - 1) * 260), Y = 120 },
            Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["milliseconds"] = Math.Clamp(recorderEvent.Text?.Length ?? 1000, 250, 30000)
            },
            Confidence = 0.75d
        };
    }

    private static Dictionary<string, object?> BuildSelectorProperties(RecorderEvent recorderEvent)
    {
        var element = recorderEvent.Element;
        var window = recorderEvent.Window;
        var mouse = recorderEvent.Mouse;
        var bounds = element?.Bounds;
        var elementAbsoluteX = element?.AbsoluteX ?? 0;
        var elementAbsoluteY = element?.AbsoluteY ?? 0;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["windowTitle"] = FirstNonEmpty(element?.WindowTitle, window?.WindowTitle),
            ["windowTitleMatch"] = "contains",
            ["processName"] = FirstNonEmpty(element?.ProcessName, window?.ProcessName),
            ["processPath"] = FirstNonEmpty(element?.ProcessPath, window?.ProcessPath),
            ["automationId"] = element?.AutomationId ?? "",
            ["elementName"] = element?.Name ?? "",
            ["controlType"] = element?.ControlType ?? "",
            ["timeoutMs"] = 5000,
            ["useRelativeFallback"] = true,
            ["useScaledFallback"] = true,
            ["useAbsoluteFallback"] = false,
            ["restoreWindowBeforeFallback"] = true,
            ["expectedWindowState"] = "normal",
            ["relativeX"] = element?.RelativeX ?? 0,
            ["relativeY"] = element?.RelativeY ?? 0,
            ["normalizedX"] = element?.NormalizedX ?? 0d,
            ["normalizedY"] = element?.NormalizedY ?? 0d,
            ["absoluteX"] = elementAbsoluteX != 0 ? elementAbsoluteX : mouse?.X ?? bounds?.X + bounds?.Width / 2 ?? 0,
            ["absoluteY"] = elementAbsoluteY != 0 ? elementAbsoluteY : mouse?.Y ?? bounds?.Y + bounds?.Height / 2 ?? 0,
            ["capturedWindowBounds"] = FormatBounds(element?.WindowBounds),
            ["capturedScreenBounds"] = FormatBounds(element?.Bounds)
        };
    }

    private static List<RecorderSuggestedConnection> ConnectSequentially(IReadOnlyList<RecorderSuggestedNode> nodes)
    {
        var connections = new List<RecorderSuggestedConnection>();
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            connections.Add(new RecorderSuggestedConnection
            {
                Id = $"draft-c{i + 1}",
                SourceNodeId = nodes[i].Id,
                SourcePort = i == 0 ? "triggered" : "out",
                TargetNodeId = nodes[i + 1].Id,
                TargetPort = "in"
            });
        }

        return connections;
    }

    private static int ComputeDraftScore(
        IReadOnlyCollection<RecorderEvent> events,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<RecorderSuggestedNode> nodes)
    {
        var score = 100;
        score -= warnings.Count * 8;
        score -= events.Count(e => e.Kind == "redactedInput") * 6;
        score -= nodes.Count(n => n.TypeId == "action.mouseClick" || n.TypeId == "action.mouseDrag") * 10;
        return Math.Clamp(score, 0, 100);
    }

    private static bool HasStrongSelector(RecorderElementContext? element, RecorderWindowContext? window)
    {
        if (element is null)
        {
            return false;
        }

        var hasStableElement = !string.IsNullOrWhiteSpace(element.AutomationId);
        var hasWindowScope = !string.IsNullOrWhiteSpace(element.WindowTitle)
            || !string.IsNullOrWhiteSpace(element.ProcessName)
            || !string.IsNullOrWhiteSpace(element.ProcessPath)
            || !string.IsNullOrWhiteSpace(window?.WindowTitle)
            || !string.IsNullOrWhiteSpace(window?.ProcessName)
            || !string.IsNullOrWhiteSpace(window?.ProcessPath);

        return hasStableElement && hasWindowScope;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static string FormatBounds(RecorderBounds? bounds)
    {
        return bounds is null
            ? ""
            : $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
    }
}
