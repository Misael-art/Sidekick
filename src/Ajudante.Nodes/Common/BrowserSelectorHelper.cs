using System.Windows.Automation;
using Ajudante.Core.Engine;

namespace Ajudante.Nodes.Common;

internal static class BrowserSelectorHelper
{
    public static (string windowTitle, string automationId, string elementName, string controlType, int timeoutMs) ResolveSelector(
        FlowExecutionContext context,
        Dictionary<string, object?> properties)
    {
        return (
            context.ResolveTemplate(NodeValueHelper.GetString(properties, "windowTitle")),
            context.ResolveTemplate(NodeValueHelper.GetString(properties, "automationId")),
            context.ResolveTemplate(NodeValueHelper.GetString(properties, "elementName")),
            context.ResolveTemplate(NodeValueHelper.GetString(properties, "controlType")),
            NodeValueHelper.GetInt(properties, "timeoutMs", 5000));
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
