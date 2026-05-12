namespace Ajudante.Core.Recorder;

public static class MacroRecorderEventCoalescer
{
    private const int TextGroupGapMs = 900;
    private const int DoubleClickMs = 500;
    private const int DoubleClickPixelTolerance = 4;
    private const int DragPixelThreshold = 8;

    public static List<RecorderEvent> Coalesce(
        IEnumerable<RecorderEvent> rawEvents,
        MacroRecorderOptions? options = null)
    {
        var effectiveOptions = options ?? new MacroRecorderOptions();
        var ordered = rawEvents
            .Where(e => ShouldKeepByCaptureOptions(e, effectiveOptions))
            .OrderBy(e => e.Timestamp)
            .Take(Math.Max(1, effectiveOptions.MaxEvents))
            .ToList();

        var result = new List<RecorderEvent>();
        var textBuffer = new TextBuffer();
        var redactedBuffer = new RedactedBuffer();
        RecorderEvent? dragStart = null;
        RecorderEvent? lastEvent = null;

        foreach (var current in ordered)
        {
            if (lastEvent is not null)
            {
                MaybeInsertPause(result, lastEvent, current, effectiveOptions);
            }

            if (!TryAppendRedacted(redactedBuffer, current))
            {
                FlushRedacted(result, redactedBuffer);
            }
            else
            {
                FlushText(result, textBuffer, effectiveOptions);
                lastEvent = current;
                continue;
            }

            if (!TryAppendText(textBuffer, current, effectiveOptions))
            {
                FlushText(result, textBuffer, effectiveOptions);
            }
            else
            {
                lastEvent = current;
                continue;
            }

            switch (current.Kind)
            {
                case "mouseMove":
                    if (dragStart is not null)
                    {
                        lastEvent = current;
                    }
                    continue;

                case "mouseDown":
                    dragStart = current;
                    lastEvent = current;
                    continue;

                case "mouseUp":
                    if (dragStart is not null)
                    {
                        var drag = BuildDragOrClick(dragStart, current);
                        AddMouseEvent(result, drag);
                        dragStart = null;
                    }
                    break;

                case "mouseClick":
                    AddMouseEvent(result, CopyEvent(current));
                    break;

                default:
                    if (!IsRawMouseFragment(current.Kind))
                    {
                        result.Add(CopyEvent(current));
                    }
                    break;
            }

            lastEvent = current;
        }

        FlushText(result, textBuffer, effectiveOptions);
        FlushRedacted(result, redactedBuffer);

        return result
            .Where(e => e.Kind != "mouseMove")
            .Take(Math.Max(1, effectiveOptions.MaxEvents))
            .ToList();
    }

    private static bool ShouldKeepByCaptureOptions(RecorderEvent current, MacroRecorderOptions options)
    {
        if (!options.CaptureMouse && current.Kind.StartsWith("mouse", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!options.CaptureKeyboard && (current.Kind == "keyPress" || current.Kind == "textInput" || current.Kind == "hotkey"))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.TargetProcessName))
        {
            var processName = current.Window?.ProcessName ?? current.Element?.ProcessName ?? "";
            return string.Equals(
                TrimExe(processName),
                TrimExe(options.TargetProcessName),
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool TryAppendText(TextBuffer buffer, RecorderEvent current, MacroRecorderOptions options)
    {
        if (!options.CaptureText || current.Kind != "keyPress")
        {
            return false;
        }

        var text = current.Keyboard?.Text ?? "";
        if (!IsPrintableText(text))
        {
            return false;
        }

        if (!buffer.IsEmpty &&
            (current.Timestamp - buffer.LastTimestamp).TotalMilliseconds > TextGroupGapMs)
        {
            return false;
        }

        if (!buffer.IsEmpty && buffer.FirstEvent is not null && !SameContext(buffer.FirstEvent, current))
        {
            return false;
        }

        buffer.Append(current, text);
        return true;
    }

    private static bool TryAppendRedacted(RedactedBuffer buffer, RecorderEvent current)
    {
        if (current.Kind != "redactedInput" || current.Text?.IsRedacted != true)
        {
            return false;
        }

        if (!buffer.IsEmpty &&
            (current.Timestamp - buffer.LastTimestamp).TotalMilliseconds > TextGroupGapMs)
        {
            return false;
        }

        if (!buffer.IsEmpty && buffer.FirstEvent is not null && !SameContext(buffer.FirstEvent, current))
        {
            return false;
        }

        buffer.Append(current, Math.Max(1, current.Text.Length));
        return true;
    }

    private static void FlushText(List<RecorderEvent> result, TextBuffer buffer, MacroRecorderOptions options)
    {
        if (buffer.IsEmpty || buffer.FirstEvent is null)
        {
            return;
        }

        var value = buffer.Text;
        var sensitiveReason = DetectSensitiveReason(value, buffer.FirstEvent.Element, buffer.FirstEvent.Window);
        var shouldRedact = !options.CaptureSensitiveText && sensitiveReason.Length > 0;
        var kind = shouldRedact ? "redactedInput" : "textInput";

        result.Add(new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Timestamp = buffer.FirstEvent.Timestamp,
            Label = shouldRedact ? "Texto sensivel redigido" : "Texto digitado",
            Window = Clone(buffer.FirstEvent.Window),
            Element = Clone(buffer.FirstEvent.Element),
            Text = new RecorderTextPayload
            {
                Value = shouldRedact ? null : value,
                Length = value.Length,
                IsRedacted = shouldRedact
            },
            Privacy = new RecorderPrivacyInfo
            {
                IsRedacted = shouldRedact,
                Mode = shouldRedact ? "redactSensitive" : "default",
                Reason = sensitiveReason
            },
            Confidence = shouldRedact ? 0.9d : 1.0d,
            Warnings = shouldRedact
                ? ["Texto sensivel nao foi armazenado. Preencha ou vincule um secret antes de executar."]
                : []
        });

        buffer.Clear();
    }

    private static void FlushRedacted(List<RecorderEvent> result, RedactedBuffer buffer)
    {
        if (buffer.IsEmpty || buffer.FirstEvent is null)
        {
            return;
        }

        result.Add(new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "redactedInput",
            Timestamp = buffer.FirstEvent.Timestamp,
            Label = "Texto sensivel redigido",
            Window = Clone(buffer.FirstEvent.Window),
            Element = Clone(buffer.FirstEvent.Element),
            Text = new RecorderTextPayload
            {
                Value = null,
                Length = buffer.Length,
                IsRedacted = true
            },
            Privacy = new RecorderPrivacyInfo
            {
                IsRedacted = true,
                Mode = "redactSensitive",
                Reason = buffer.FirstEvent.Privacy.Reason
            },
            Confidence = 0.9d,
            Warnings = ["Texto sensivel nao foi armazenado. Preencha ou vincule um secret antes de executar."]
        });

        buffer.Clear();
    }

    private static void MaybeInsertPause(
        ICollection<RecorderEvent> result,
        RecorderEvent previous,
        RecorderEvent current,
        MacroRecorderOptions options)
    {
        if (options.IdlePauseMs <= 0)
        {
            return;
        }

        var gap = current.Timestamp - previous.Timestamp;
        if (gap.TotalMilliseconds < options.IdlePauseMs)
        {
            return;
        }

        result.Add(new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "pause",
            Timestamp = previous.Timestamp.AddMilliseconds(options.IdlePauseMs),
            Label = "Pausa detectada",
            Text = new RecorderTextPayload { Value = null, Length = (int)gap.TotalMilliseconds },
            Confidence = 1.0d
        });
    }

    private static RecorderEvent BuildDragOrClick(RecorderEvent start, RecorderEvent end)
    {
        var startX = start.Mouse?.X ?? 0;
        var startY = start.Mouse?.Y ?? 0;
        var endX = end.Mouse?.X ?? startX;
        var endY = end.Mouse?.Y ?? startY;
        var moved = Math.Abs(endX - startX) >= DragPixelThreshold || Math.Abs(endY - startY) >= DragPixelThreshold;

        return new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = moved ? "mouseDrag" : "mouseClick",
            Timestamp = start.Timestamp,
            Window = Clone(start.Window) ?? Clone(end.Window),
            Element = Clone(start.Element) ?? Clone(end.Element),
            Mouse = moved
                ? new RecorderMousePayload
                {
                    StartX = startX,
                    StartY = startY,
                    EndX = endX,
                    EndY = endY,
                    X = endX,
                    Y = endY,
                    Button = start.Mouse?.Button ?? end.Mouse?.Button ?? "left"
                }
                : new RecorderMousePayload
                {
                    X = endX,
                    Y = endY,
                    Button = start.Mouse?.Button ?? end.Mouse?.Button ?? "left"
                },
            Confidence = moved ? 0.95d : 1.0d
        };
    }

    private static void AddMouseEvent(List<RecorderEvent> result, RecorderEvent current)
    {
        var last = result.LastOrDefault(e => e.Kind == "mouseClick");
        if (last is not null && IsDoubleClickPair(last, current))
        {
            result.Remove(last);
            result.Add(new RecorderEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = "mouseDoubleClick",
                Timestamp = last.Timestamp,
                Window = Clone(current.Window) ?? Clone(last.Window),
                Element = Clone(current.Element) ?? Clone(last.Element),
                Mouse = new RecorderMousePayload
                {
                    X = ((last.Mouse?.X ?? 0) + (current.Mouse?.X ?? 0)) / 2,
                    Y = ((last.Mouse?.Y ?? 0) + (current.Mouse?.Y ?? 0)) / 2,
                    Button = current.Mouse?.Button ?? last.Mouse?.Button ?? "left"
                },
                Confidence = Math.Min(last.Confidence, current.Confidence)
            });
            return;
        }

        result.Add(current);
    }

    private static bool IsDoubleClickPair(RecorderEvent first, RecorderEvent second)
    {
        if (first.Kind != "mouseClick" || second.Kind != "mouseClick")
        {
            return false;
        }

        if (!SameWindow(first.Window, second.Window))
        {
            return false;
        }

        var firstMouse = first.Mouse;
        var secondMouse = second.Mouse;
        if (firstMouse is null || secondMouse is null)
        {
            return false;
        }

        return string.Equals(firstMouse.Button, secondMouse.Button, StringComparison.OrdinalIgnoreCase)
            && (second.Timestamp - first.Timestamp).TotalMilliseconds <= DoubleClickMs
            && Math.Abs(firstMouse.X - secondMouse.X) <= DoubleClickPixelTolerance
            && Math.Abs(firstMouse.Y - secondMouse.Y) <= DoubleClickPixelTolerance;
    }

    private static bool SameContext(RecorderEvent first, RecorderEvent second)
    {
        return SameWindow(first.Window, second.Window)
            && string.Equals(first.Element?.AutomationId ?? "", second.Element?.AutomationId ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.Element?.Name ?? "", second.Element?.Name ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameWindow(RecorderWindowContext? first, RecorderWindowContext? second)
    {
        return string.Equals(first?.WindowTitle ?? "", second?.WindowTitle ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(TrimExe(first?.ProcessName), TrimExe(second?.ProcessName), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRawMouseFragment(string kind)
    {
        return kind is "mouseDown" or "mouseUp" or "mouseMove";
    }

    private static bool IsPrintableText(string value)
    {
        if (value.Length != 1)
        {
            return false;
        }

        return !char.IsControl(value[0]);
    }

    private static string DetectSensitiveReason(
        string text,
        RecorderElementContext? element,
        RecorderWindowContext? window)
    {
        var haystack = string.Join(" ", new[]
        {
            text,
            element?.Name,
            element?.AutomationId,
            element?.ClassName,
            element?.ControlType,
            element?.WindowTitle,
            window?.WindowTitle
        }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();

        var sensitiveTerms = new[]
        {
            "password",
            "passwd",
            "pwd",
            "senha",
            "token",
            "secret",
            "api key",
            "apikey",
            "access key",
            "chave"
        };

        if (sensitiveTerms.Any(haystack.Contains) || text.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            return "Campo ou conteudo parece conter senha, token ou API key.";
        }

        return "";
    }

    private static RecorderEvent CopyEvent(RecorderEvent source)
    {
        return new RecorderEvent
        {
            Id = source.Id,
            Kind = source.Kind,
            Timestamp = source.Timestamp,
            Label = source.Label,
            Window = Clone(source.Window),
            Element = Clone(source.Element),
            Mouse = Clone(source.Mouse),
            Keyboard = Clone(source.Keyboard),
            Text = Clone(source.Text),
            Privacy = Clone(source.Privacy),
            Confidence = source.Confidence,
            Warnings = [.. source.Warnings]
        };
    }

    private static RecorderWindowContext? Clone(RecorderWindowContext? source)
    {
        return source is null
            ? null
            : new RecorderWindowContext
            {
                WindowTitle = source.WindowTitle,
                ProcessName = source.ProcessName,
                ProcessPath = source.ProcessPath,
                ProcessId = source.ProcessId,
                WindowHandle = source.WindowHandle
            };
    }

    private static RecorderElementContext? Clone(RecorderElementContext? source)
    {
        return source is null
            ? null
            : new RecorderElementContext
            {
                AutomationId = source.AutomationId,
                Name = source.Name,
                ClassName = source.ClassName,
                ControlType = source.ControlType,
                WindowTitle = source.WindowTitle,
                ProcessName = source.ProcessName,
                ProcessPath = source.ProcessPath,
                ProcessId = source.ProcessId,
                Bounds = Clone(source.Bounds),
                WindowBounds = Clone(source.WindowBounds),
                RelativeX = source.RelativeX,
                RelativeY = source.RelativeY,
                NormalizedX = source.NormalizedX,
                NormalizedY = source.NormalizedY,
                AbsoluteX = source.AbsoluteX,
                AbsoluteY = source.AbsoluteY,
                CursorPixelColor = source.CursorPixelColor,
                DetectedText = source.DetectedText,
                CurrentText = source.CurrentText,
                PlaceholderText = source.PlaceholderText,
                SelectorStrength = source.SelectorStrength,
                SelectorStrategy = source.SelectorStrategy,
                IsBrowserSurface = source.IsBrowserSurface,
                BrowserUrl = source.BrowserUrl,
                BrowserOrigin = source.BrowserOrigin,
                BrowserDocumentTitle = source.BrowserDocumentTitle
            };
    }

    private static RecorderBounds? Clone(RecorderBounds? source)
    {
        return source is null
            ? null
            : new RecorderBounds
            {
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
    }

    private static RecorderMousePayload? Clone(RecorderMousePayload? source)
    {
        return source is null
            ? null
            : new RecorderMousePayload
            {
                X = source.X,
                Y = source.Y,
                StartX = source.StartX,
                StartY = source.StartY,
                EndX = source.EndX,
                EndY = source.EndY,
                Delta = source.Delta,
                Button = source.Button
            };
    }

    private static RecorderKeyboardPayload? Clone(RecorderKeyboardPayload? source)
    {
        return source is null
            ? null
            : new RecorderKeyboardPayload
            {
                Key = source.Key,
                Text = source.Text,
                Modifiers = [.. source.Modifiers]
            };
    }

    private static RecorderTextPayload? Clone(RecorderTextPayload? source)
    {
        return source is null
            ? null
            : new RecorderTextPayload
            {
                Value = source.Value,
                Length = source.Length,
                IsRedacted = source.IsRedacted
            };
    }

    private static RecorderPrivacyInfo Clone(RecorderPrivacyInfo? source)
    {
        return source is null
            ? new RecorderPrivacyInfo()
            : new RecorderPrivacyInfo
            {
                IsRedacted = source.IsRedacted,
                Mode = source.Mode,
                Reason = source.Reason
            };
    }

    private static string TrimExe(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? text[..^4]
            : text;
    }

    private sealed class TextBuffer
    {
        private readonly List<string> _parts = [];

        public RecorderEvent? FirstEvent { get; private set; }
        public DateTime LastTimestamp { get; private set; }
        public string Text => string.Concat(_parts);
        public bool IsEmpty => FirstEvent is null;

        public void Append(RecorderEvent current, string text)
        {
            FirstEvent ??= current;
            LastTimestamp = current.Timestamp;
            _parts.Add(text);
        }

        public void Clear()
        {
            FirstEvent = null;
            LastTimestamp = default;
            _parts.Clear();
        }
    }

    private sealed class RedactedBuffer
    {
        public RecorderEvent? FirstEvent { get; private set; }
        public DateTime LastTimestamp { get; private set; }
        public int Length { get; private set; }
        public bool IsEmpty => FirstEvent is null;

        public void Append(RecorderEvent current, int length)
        {
            FirstEvent ??= current;
            LastTimestamp = current.Timestamp;
            Length += length;
        }

        public void Clear()
        {
            FirstEvent = null;
            LastTimestamp = default;
            Length = 0;
        }
    }
}
