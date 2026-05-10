using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace Ajudante.Platform.UIAutomation;

public static class AutomationElementLocator
{
    public enum TitleMatch
    {
        Equals,
        Contains,
        Regex
    }

    public static AutomationElement? FindElement(
        string? windowTitle,
        string? automationId,
        string? name,
        string? controlType,
        int timeoutMs = 0)
    {
        return FindElement(
            windowTitle,
            automationId,
            name,
            controlType,
            timeoutMs,
            processName: null,
            processPath: null,
            titleMatch: TitleMatch.Equals);
    }

    public static AutomationElement? FindElement(
        string? windowTitle,
        string? automationId,
        string? name,
        string? controlType,
        int timeoutMs,
        string? processName,
        string? processPath,
        TitleMatch titleMatch,
        TitleMatch nameMatch = TitleMatch.Equals,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Environment.TickCount64;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = FindElementOnce(
                windowTitle,
                automationId,
                name,
                controlType,
                processName,
                processPath,
                titleMatch,
                nameMatch);
            if (element is not null)
                return element;

            if (timeoutMs <= 0)
                break;

            var elapsed = Environment.TickCount64 - startedAt;
            var remaining = Math.Max(0, timeoutMs - elapsed);
            var sleepMs = (int)Math.Min(200, remaining);
            if (sleepMs <= 0)
                break;

            if (cancellationToken.WaitHandle.WaitOne(sleepMs))
                throw new OperationCanceledException(cancellationToken);
        }
        while (Environment.TickCount64 - startedAt < timeoutMs);

        return null;
    }

    public static TitleMatch ParseTitleMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TitleMatch.Equals;

        return value.Trim().ToLowerInvariant() switch
        {
            "contains" or "substring" => TitleMatch.Contains,
            "regex" or "pattern" => TitleMatch.Regex,
            _ => TitleMatch.Equals
        };
    }

    public static bool TitleMatches(string? candidate, string? pattern, TitleMatch mode)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        candidate ??= "";
        return mode switch
        {
            TitleMatch.Contains => candidate.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatch.Regex => SafeRegexMatch(candidate, pattern),
            _ => string.Equals(candidate, pattern, StringComparison.OrdinalIgnoreCase)
        };
    }

    public static string ExtractText(AutomationElement element)
    {
        var name = string.Empty;
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
                valuePatternObject is ValuePattern valuePattern)
            {
                var value = valuePattern.Current.Value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
        }

        try
        {
            name = element.Current.Name ?? string.Empty;
        }
        catch
        {
        }

        var descendantText = ExtractDescendantText(element);
        if (string.IsNullOrWhiteSpace(descendantText))
            return name;

        return string.IsNullOrWhiteSpace(name)
            ? descendantText
            : $"{name}{Environment.NewLine}{descendantText}";
    }

    public static bool Invoke(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObject) &&
                invokePatternObject is InvokePattern invokePattern)
            {
                invokePattern.Invoke();
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public static bool Focus(AutomationElement element)
    {
        try
        {
            element.SetFocus();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement? FindElementOnce(
        string? windowTitle,
        string? automationId,
        string? name,
        string? controlType,
        string? processName,
        string? processPath,
        TitleMatch titleMatch,
        TitleMatch nameMatch)
    {
        try
        {
            var root = AutomationElement.RootElement;
            if (root is null)
                return null;

            var window = FindWindow(root, windowTitle, processName, processPath, titleMatch);
            if (window is null)
                return null;

            if (string.IsNullOrWhiteSpace(automationId) &&
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(controlType))
            {
                return window;
            }

            var conditions = new List<Condition>();
            if (!string.IsNullOrWhiteSpace(automationId))
                conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            if (!string.IsNullOrWhiteSpace(controlType) && TryParseControlType(controlType, out var parsedControlType))
                conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, parsedControlType));

            if (conditions.Count == 0 && string.IsNullOrWhiteSpace(name))
                return window;

            var condition = conditions.Count switch
            {
                0 => Condition.TrueCondition,
                1 => conditions[0],
                _ => new AndCondition(conditions.ToArray())
            };

            var candidates = window.FindAll(TreeScope.Descendants, condition);
            if (candidates is null || candidates.Count == 0)
                return null;

            foreach (AutomationElement candidate in candidates)
            {
                if (MatchesElementName(candidate, name, nameMatch))
                    return candidate;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static AutomationElement? FindWindow(
        AutomationElement root,
        string? windowTitle,
        string? processName,
        string? processPath,
        TitleMatch titleMatch)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(windowTitle);
        var hasProcessName = !string.IsNullOrWhiteSpace(processName);
        var hasProcessPath = !string.IsNullOrWhiteSpace(processPath);

        if (!hasTitle && !hasProcessName && !hasProcessPath)
            return root;

        var windows = root.FindAll(
            TreeScope.Children,
            Condition.TrueCondition);

        if (windows is null || windows.Count == 0)
            return null;

        AutomationElement? fallback = null;
        foreach (AutomationElement candidate in windows)
        {
            if (!MatchesProcess(candidate, processName, processPath))
                continue;

            string candidateTitle;
            try
            {
                candidateTitle = candidate.Current.Name ?? "";
            }
            catch
            {
                continue;
            }

            if (hasTitle && !TitleMatches(candidateTitle, windowTitle, titleMatch))
                continue;

            if (!hasTitle && string.IsNullOrWhiteSpace(candidateTitle))
            {
                fallback ??= candidate;
                continue;
            }

            return candidate;
        }

        return fallback;
    }

    private static bool MatchesProcess(AutomationElement candidate, string? processName, string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(processPath))
            return true;

        int processId;
        try
        {
            processId = (int)candidate.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty);
        }
        catch
        {
            return false;
        }

        if (processId <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId);

            if (!string.IsNullOrWhiteSpace(processName))
            {
                var expectedProcessName = processName.Trim();
                if (expectedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    expectedProcessName = expectedProcessName[..^4];

                if (!string.Equals(process.ProcessName, expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(processPath))
            {
                string? actualPath = null;
                try
                {
                    actualPath = process.MainModule?.FileName;
                }
                catch
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(actualPath))
                    return false;

                if (!string.Equals(
                        Path.GetFullPath(actualPath),
                        Path.GetFullPath(processPath),
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool SafeRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchesElementName(AutomationElement element, string? name, TitleMatch nameMatch)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        try
        {
            return TitleMatches(element.Current.Name ?? string.Empty, name, nameMatch);
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractDescendantText(AutomationElement element)
    {
        try
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            var descendants = element.FindAll(TreeScope.Descendants, condition);
            if (descendants is null || descendants.Count == 0)
                return string.Empty;

            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < descendants.Count && values.Count < 80; i++)
            {
                if (descendants[i] is not AutomationElement child)
                    continue;

                string text;
                try
                {
                    text = child.Current.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text) &&
                        child.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
                        valuePatternObject is ValuePattern valuePattern)
                    {
                        text = valuePattern.Current.Value ?? string.Empty;
                    }
                }
                catch
                {
                    continue;
                }

                text = text.Trim();
                if (text.Length == 0 || !seen.Add(text))
                    continue;

                values.Add(text);
            }

            var joined = string.Join(Environment.NewLine, values);
            return joined.Length <= 4096 ? joined : joined[..4096];
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryParseControlType(string value, out ControlType controlType)
    {
        var normalized = value.Trim().ToLowerInvariant();
        controlType = normalized switch
        {
            "button" => ControlType.Button,
            "edit" => ControlType.Edit,
            "text" => ControlType.Text,
            "document" => ControlType.Document,
            "hyperlink" => ControlType.Hyperlink,
            "pane" => ControlType.Pane,
            "listitem" => ControlType.ListItem,
            "list" => ControlType.List,
            "combobox" => ControlType.ComboBox,
            "checkbox" => ControlType.CheckBox,
            "group" => ControlType.Group,
            "tabitem" => ControlType.TabItem,
            "tab" => ControlType.TabItem,
            "menuitem" => ControlType.MenuItem,
            "window" => ControlType.Window,
            _ => ControlType.Custom
        };

        return controlType != ControlType.Custom || normalized == "custom";
    }
}
