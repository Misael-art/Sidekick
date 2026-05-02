using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.UIAutomation;
using Ajudante.Platform.Windows;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.windowControl",
    DisplayName = "Window Control",
    Category = NodeCategory.Action,
    Description = "Focuses, brings forward, minimizes, maximizes, restores, or gracefully closes a target desktop window",
    Color = "#22C55E")]
public class WindowControlNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.windowControl",
        DisplayName = "Window Control",
        Category = NodeCategory.Action,
        Description = "Focuses, brings forward, minimizes, maximizes, restores, or gracefully closes a target desktop window",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Done", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions("window")
            .Concat(new[]
            {
                new PropertyDefinition { Id = "operation", Name = "Operation", Type = PropertyType.Dropdown, DefaultValue = "focus", Description = "Window operation to perform", Options = new[] { "focus", "bringToFront", "minimize", "maximize", "restore", "close" } }
            })
            .ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var selector = BrowserSelectorHelper.ResolveSelector(context, _properties);
        context.EmitPhase(RuntimePhases.WaitingForWindow, "Waiting for target desktop window", new Dictionary<string, object?>
        {
            ["windowTitle"] = selector.windowTitle,
            ["processName"] = selector.processName,
            ["processPath"] = selector.processPath
        });

        var window = AutomationElementLocator.FindElement(
            selector.windowTitle,
            automationId: "",
            name: "",
            controlType: "",
            selector.timeoutMs,
            selector.processName,
            selector.processPath,
            selector.windowTitleMatch);
        if (window is null)
        {
            return Task.FromResult(NodeResult.Ok("notFound", new Dictionary<string, object?>
            {
                ["reason"] = "Window not found"
            }));
        }

        var hwndValue = window.Current.NativeWindowHandle;
        var hwnd = hwndValue == 0 ? IntPtr.Zero : new IntPtr(hwndValue);
        var operation = NodeValueHelper.GetString(_properties, "operation", "focus");
        var success = operation switch
        {
            "bringToFront" => WindowController.BringToFront(hwnd),
            "minimize" => WindowController.Minimize(hwnd),
            "maximize" => WindowController.Maximize(hwnd),
            "restore" => WindowController.Restore(hwnd),
            "close" => WindowController.Close(hwnd),
            _ => WindowController.Focus(hwnd)
        };

        return Task.FromResult(success
            ? NodeResult.Ok("out", new Dictionary<string, object?> { ["operation"] = operation })
            : NodeResult.Fail($"Window operation failed: {operation}"));
    }
}
