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
    AutomationElementLocator.TitleMatch windowTitleMatch,
    bool useRelativeFallback,
    bool useScaledFallback,
    bool useAbsoluteFallback,
    bool restoreWindowBeforeFallback,
    string expectedWindowState,
    int relativeX,
    int relativeY,
    double normalizedX,
    double normalizedY,
    int absoluteX,
    int absoluteY);

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
            processPath: ExpandEnvironmentPath(context.ResolveTemplate(NodeValueHelper.GetString(properties, "processPath"))),
            windowTitleMatch: AutomationElementLocator.ParseTitleMatch(NodeValueHelper.GetString(properties, "windowTitleMatch")),
            useRelativeFallback: NodeValueHelper.GetBool(properties, "useRelativeFallback", true),
            useScaledFallback: NodeValueHelper.GetBool(properties, "useScaledFallback", true),
            useAbsoluteFallback: NodeValueHelper.GetBool(properties, "useAbsoluteFallback", true),
            restoreWindowBeforeFallback: NodeValueHelper.GetBool(properties, "restoreWindowBeforeFallback", true),
            expectedWindowState: context.ResolveTemplate(NodeValueHelper.GetString(properties, "expectedWindowState", "normal")),
            relativeX: NodeValueHelper.GetInt(properties, "relativeX", 0),
            relativeY: NodeValueHelper.GetInt(properties, "relativeY", 0),
            normalizedX: NodeValueHelper.GetDouble(properties, "normalizedX", 0),
            normalizedY: NodeValueHelper.GetDouble(properties, "normalizedY", 0),
            absoluteX: NodeValueHelper.GetInt(properties, "absoluteX", 0),
            absoluteY: NodeValueHelper.GetInt(properties, "absoluteY", 0));
    }

    private static string ExpandEnvironmentPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? ""
            : Environment.ExpandEnvironmentVariables(path);
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
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum wait time for the element" },
            new() { Id = "useRelativeFallback", Name = "Use Relative Fallback", Type = PropertyType.Boolean, DefaultValue = true, Description = "Enable selector -> relativeWindow fallback phase" },
            new() { Id = "useScaledFallback", Name = "Use Scaled Fallback", Type = PropertyType.Boolean, DefaultValue = true, Description = "Enable scaled-screen fallback phase" },
            new() { Id = "useAbsoluteFallback", Name = "Use Absolute Fallback", Type = PropertyType.Boolean, DefaultValue = true, Description = "Enable absolute desktop fallback phase" },
            new() { Id = "restoreWindowBeforeFallback", Name = "Restore Window Before Fallback", Type = PropertyType.Boolean, DefaultValue = true, Description = "Restore/focus target window before fallback click phases" },
            new() { Id = "expectedWindowState", Name = "Expected Window State", Type = PropertyType.Dropdown, DefaultValue = "normal", Description = "Expected captured state", Options = new[] { "normal", "maximized", "minimized" } },
            new() { Id = "capturedWindowBounds", Name = "Captured Window Bounds", Type = PropertyType.String, DefaultValue = "", Description = "Diagnostic: original captured window bounds" },
            new() { Id = "capturedScreenBounds", Name = "Captured Screen Bounds", Type = PropertyType.String, DefaultValue = "", Description = "Diagnostic: original captured screen bounds" },
            new() { Id = "relativeX", Name = "Relative X", Type = PropertyType.Integer, DefaultValue = 0, Description = "X offset captured relative to target window" },
            new() { Id = "relativeY", Name = "Relative Y", Type = PropertyType.Integer, DefaultValue = 0, Description = "Y offset captured relative to target window" },
            new() { Id = "normalizedX", Name = "Normalized X", Type = PropertyType.Float, DefaultValue = 0.0, Description = "Normalized horizontal position (0..1)" },
            new() { Id = "normalizedY", Name = "Normalized Y", Type = PropertyType.Float, DefaultValue = 0.0, Description = "Normalized vertical position (0..1)" },
            new() { Id = "absoluteX", Name = "Absolute X", Type = PropertyType.Integer, DefaultValue = 0, Description = "Absolute desktop X captured at design time" },
            new() { Id = "absoluteY", Name = "Absolute Y", Type = PropertyType.Integer, DefaultValue = 0, Description = "Absolute desktop Y captured at design time" }
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
