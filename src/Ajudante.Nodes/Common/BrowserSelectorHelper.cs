using System.Windows.Automation;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Common;

internal sealed record DesktopSelector(
    string windowTitle,
    string automationId,
    string elementName,
    string controlType,
    int timeoutMs,
    string processName,
    string processPath,
    AutomationElementLocator.TitleMatch windowTitleMatch);

internal static class BrowserSelectorHelper
{
    public static DesktopSelector ResolveSelector(
        FlowExecutionContext context,
        Dictionary<string, object?> properties)
    {
        return new DesktopSelector(
            windowTitle: context.ResolveTemplate(NodeValueHelper.GetString(properties, "windowTitle")),
            automationId: context.ResolveTemplate(NodeValueHelper.GetString(properties, "automationId")),
            elementName: context.ResolveTemplate(NodeValueHelper.GetString(properties, "elementName")),
            controlType: context.ResolveTemplate(NodeValueHelper.GetString(properties, "controlType")),
            timeoutMs: NodeValueHelper.GetInt(properties, "timeoutMs", 5000),
            processName: context.ResolveTemplate(NodeValueHelper.GetString(properties, "processName")),
            processPath: context.ResolveTemplate(NodeValueHelper.GetString(properties, "processPath")),
            windowTitleMatch: AutomationElementLocator.ParseTitleMatch(NodeValueHelper.GetString(properties, "windowTitleMatch")));
    }

    public static AutomationElement? FindElement(DesktopSelector selector)
    {
        return AutomationElementLocator.FindElement(
            selector.windowTitle,
            selector.automationId,
            selector.elementName,
            selector.controlType,
            selector.timeoutMs,
            selector.processName,
            selector.processPath,
            selector.windowTitleMatch);
    }

    public static List<PropertyDefinition> SelectorPropertyDefinitions(string defaultControlType = "")
    {
        return new List<PropertyDefinition>
        {
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "", Description = "Optional desktop window title" },
            new() { Id = "windowTitleMatch", Name = "Window Title Match", Type = PropertyType.Dropdown, DefaultValue = "equals", Description = "How to match the window title", Options = new[] { "equals", "contains", "regex" } },
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "", Description = "Optional process name, with or without .exe" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Optional full executable path for the target process" },
            new() { Id = "automationId", Name = "Automation ID", Type = PropertyType.String, DefaultValue = "", Description = "Optional UIAutomation automation id" },
            new() { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "", Description = "Visible element name/text" },
            new() { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = defaultControlType, Description = "Optional UIAutomation control type, e.g. button, edit, text" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum wait time for the element" }
        };
    }

    public static Dictionary<string, object?> BuildSelectorOutputs(AutomationElement element)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = element.Current.Name,
            ["automationId"] = element.Current.AutomationId,
            ["controlType"] = element.Current.ControlType.ProgrammaticName
        };
    }
}
