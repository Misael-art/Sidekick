using System.Windows.Automation;

namespace Ajudante.Platform.UIAutomation;

public static class AutomationElementLocator
{
    public static AutomationElement? FindElement(
        string? windowTitle,
        string? automationId,
        string? name,
        string? controlType,
        int timeoutMs = 0)
    {
        var startedAt = Environment.TickCount64;

        do
        {
            var element = FindElementOnce(windowTitle, automationId, name, controlType);
            if (element is not null)
                return element;

            if (timeoutMs <= 0)
                break;

            Thread.Sleep(200);
        }
        while (Environment.TickCount64 - startedAt < timeoutMs);

        return null;
    }

    public static string ExtractText(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
                valuePatternObject is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value ?? string.Empty;
            }
        }
        catch
        {
        }

        try
        {
            return element.Current.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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
        string? controlType)
    {
        try
        {
            var root = AutomationElement.RootElement;
            if (root is null)
                return null;

            var window = FindWindow(root, windowTitle);
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
            if (!string.IsNullOrWhiteSpace(name))
                conditions.Add(new PropertyCondition(AutomationElement.NameProperty, name));
            if (!string.IsNullOrWhiteSpace(controlType) && TryParseControlType(controlType, out var parsedControlType))
                conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, parsedControlType));

            if (conditions.Count == 0)
                return window;

            var condition = conditions.Count == 1 ? conditions[0] : new AndCondition(conditions.ToArray());
            return window.FindFirst(TreeScope.Descendants, condition);
        }
        catch
        {
            return null;
        }
    }

    private static AutomationElement? FindWindow(AutomationElement root, string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return root;

        return root.FindFirst(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.NameProperty, windowTitle));
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
            "combobox" => ControlType.ComboBox,
            "checkbox" => ControlType.CheckBox,
            "window" => ControlType.Window,
            _ => ControlType.Custom
        };

        return controlType != ControlType.Custom || normalized == "custom";
    }
}
