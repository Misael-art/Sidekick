using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Input;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.browserClick",
    DisplayName = "Browser Click",
    Category = NodeCategory.Action,
    Description = "Finds a browser UI element and clicks it",
    Color = "#22C55E")]
public class BrowserClickNode : IActionNode
{
    private Dictionary<string, object?> _properties = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.browserClick",
        DisplayName = "Browser Click",
        Category = NodeCategory.Action,
        Description = "Finds a browser UI element and clicks it",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "clickedName", Name = "Clicked Name", DataType = PortDataType.String }
        },
        Properties = BrowserPropertyDefinitions()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = properties;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        var clickType = NodeValueHelper.GetString(_properties, "clickType", "single");
        var element = BrowserSelectorHelper.FindElement(selector);
        if (element is null)
            return Task.FromResult(NodeResult.Fail("Browser element not found"));

        if (!AutomationElementLocator.Invoke(element))
        {
            var rect = element.Current.BoundingRectangle;
            var centerX = (int)(rect.Left + rect.Width / 2);
            var centerY = (int)(rect.Top + rect.Height / 2);
            MouseSimulator.MoveTo(centerX, centerY);
            Thread.Sleep(100);
            if (string.Equals(clickType, "double", StringComparison.OrdinalIgnoreCase))
                MouseSimulator.DoubleClick();
            else
                MouseSimulator.Click();
        }

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["clickedName"] = element.Current.Name
        }));
    }

    private static List<PropertyDefinition> BrowserPropertyDefinitions()
    {
        return new List<PropertyDefinition>
        {
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "", Description = "Optional desktop window title" },
            new() { Id = "windowTitleMatch", Name = "Window Title Match", Type = PropertyType.Dropdown, DefaultValue = "equals", Description = "How to match the window title", Options = new[] { "equals", "contains", "regex" } },
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "", Description = "Optional process name, with or without .exe" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Optional full executable path for the target process" },
            new() { Id = "automationId", Name = "Automation ID", Type = PropertyType.String, DefaultValue = "", Description = "Optional automation id" },
            new() { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "", Description = "Visible element name/text" },
            new() { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = "", Description = "Optional UIAutomation control type" },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 5000, Description = "Maximum wait time for the element" },
            new() { Id = "clickType", Name = "Click Type", Type = PropertyType.Dropdown, DefaultValue = "single", Description = "Single or double click", Options = new[] { "single", "double" } }
        };
    }
}
