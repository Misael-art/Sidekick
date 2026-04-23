using System.Windows.Automation;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Input;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.browserType",
    DisplayName = "Browser Type",
    Category = NodeCategory.Action,
    Description = "Finds a browser UI element and types text into it",
    Color = "#22C55E")]
public class BrowserTypeNode : IActionNode
{
    private Dictionary<string, object?> _properties = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.browserType",
        DisplayName = "Browser Type",
        Category = NodeCategory.Action,
        Description = "Finds a browser UI element and types text into it",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "typedText", Name = "Typed Text", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "", Description = "Optional browser window title" },
            new() { Id = "automationId", Name = "Automation ID", Type = PropertyType.String, DefaultValue = "", Description = "Optional automation id" },
            new() { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "", Description = "Visible element name/text" },
            new() { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = "edit", Description = "Optional UIAutomation control type" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum wait time for the element" },
            new() { Id = "text", Name = "Text", Type = PropertyType.String, DefaultValue = "", Description = "Text to type (supports {{variable}} templates)" },
            new() { Id = "clearExisting", Name = "Clear Existing", Type = PropertyType.Boolean, DefaultValue = false, Description = "Clear existing content before typing" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = properties;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var resolvedText = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "text"));
        var clearExisting = NodeValueHelper.GetBool(_properties, "clearExisting");

        var element = AutomationElementLocator.FindElement(selector.windowTitle, selector.automationId, selector.elementName, selector.controlType, selector.timeoutMs);
        if (element is null)
            return Task.FromResult(NodeResult.Fail("Browser element not found"));

        AutomationElementLocator.Focus(element);

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern &&
            !valuePattern.Current.IsReadOnly)
        {
            valuePattern.SetValue(clearExisting ? resolvedText : (valuePattern.Current.Value ?? string.Empty) + resolvedText);
        }
        else
        {
            if (clearExisting)
                KeyboardSimulator.PressCombo(VirtualKey.VK_CONTROL, VirtualKey.VK_A);

            KeyboardSimulator.TypeText(resolvedText);
        }

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["typedText"] = resolvedText
        }));
    }
}
