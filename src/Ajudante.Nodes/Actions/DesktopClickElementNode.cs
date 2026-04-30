using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Input;
using Ajudante.Platform.UIAutomation;
using Ajudante.Platform.Windows;
using System.Drawing;
using System.Windows.Automation;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.desktopClickElement",
    DisplayName = "Desktop Click Element",
    Category = NodeCategory.Action,
    Description = "Finds a Windows desktop element and clicks it with selector-first fallback",
    Color = "#22C55E")]
public class DesktopClickElementNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.desktopClickElement",
        DisplayName = "Desktop Click Element",
        Category = NodeCategory.Action,
        Description = "Finds a Windows desktop element and clicks it with selector-first fallback",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Clicked", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "clickedName", Name = "Clicked Name", DataType = PortDataType.String },
            new() { Id = "fallbackUsed", Name = "Fallback Used", DataType = PortDataType.Boolean }
        },
        Properties = BrowserSelectorHelper.SelectorPropertyDefinitions("button")
            .Concat(new[]
            {
                new PropertyDefinition { Id = "clickType", Name = "Click Type", Type = PropertyType.Dropdown, DefaultValue = "single", Description = "Single or double click", Options = new[] { "single", "double" } }
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
        var clickType = NodeValueHelper.GetString(_properties, "clickType", "single");
        context.EmitPhase(RuntimePhases.WaitingForElement, "Waiting for element before click");
        var element = BrowserSelectorHelper.FindElement(selector);

        if (element is not null)
        {
            var fallbackUsed = false;
            context.EmitPhase(RuntimePhases.ElementMatched, "selector matched");
            if (!AutomationElementLocator.Invoke(element))
            {
                fallbackUsed = true;
                context.EmitPhase(RuntimePhases.FallbackVisualActive, "relative fallback active");
                if (!TryCoordinateClickWithinBounds(element, clickType))
                {
                    return Task.FromResult(NodeResult.Ok("notFound", new Dictionary<string, object?>
                    {
                        ["clicked"] = false,
                        ["reason"] = "Selector found but target bounds were outside safe click region"
                    }));
                }
            }

            context.EmitPhase(RuntimePhases.ClickExecuted, "Desktop click executed", new { fallbackUsed });
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["clickedName"] = element.Current.Name,
                ["fallbackUsed"] = fallbackUsed
            }));
        }

        if (selector.restoreWindowBeforeFallback)
            TryRestoreWindow(selector);

        if (selector.useRelativeFallback && TryRelativeFallbackClick(selector, clickType))
        {
            context.EmitPhase(RuntimePhases.FallbackVisualActive, "relative fallback active");
            context.EmitPhase(RuntimePhases.ClickExecuted, "Desktop click executed", new { fallbackUsed = true, phase = "relativeWindow" });
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["clickedName"] = "",
                ["fallbackUsed"] = true
            }));
        }

        if (selector.useScaledFallback && TryScaledFallbackClick(selector, clickType))
        {
            context.EmitPhase(RuntimePhases.FallbackVisualActive, "scaled fallback active");
            context.EmitPhase(RuntimePhases.ClickExecuted, "Desktop click executed", new { fallbackUsed = true, phase = "scaledScreen" });
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["clickedName"] = "",
                ["fallbackUsed"] = true
            }));
        }

        if (selector.useAbsoluteFallback && TryAbsoluteFallbackClick(selector, clickType))
        {
            context.EmitPhase(RuntimePhases.FallbackVisualActive, "absolute fallback active");
            context.EmitPhase(RuntimePhases.ClickExecuted, "Desktop click executed", new { fallbackUsed = true, phase = "absoluteDesktop" });
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["clickedName"] = "",
                ["fallbackUsed"] = true
            }));
        }

        context.EmitPhase(RuntimePhases.Error, "notFound");
        return Task.FromResult(NodeResult.Ok("notFound", new Dictionary<string, object?>
        {
            ["clicked"] = false,
            ["reason"] = "Element not found"
        }));
    }

    private static bool TryCoordinateClickWithinBounds(AutomationElement element, string clickType)
    {
        var rect = element.Current.BoundingRectangle;
        var centerX = (int)(rect.Left + rect.Width / 2);
        var centerY = (int)(rect.Top + rect.Height / 2);
        if (!IsPointSafe(centerX, centerY))
            return false;

        MouseSimulator.MoveTo(centerX, centerY);
        Thread.Sleep(100);
        if (string.Equals(clickType, "double", StringComparison.OrdinalIgnoreCase))
            MouseSimulator.DoubleClick();
        else
            MouseSimulator.Click();

        return true;
    }

    private static bool TryRelativeFallbackClick(DesktopSelector selector, string clickType)
    {
        var window = AutomationElementLocator.FindElement(
            selector.windowTitle,
            automationId: "",
            name: "",
            controlType: "window",
            selector.timeoutMs,
            selector.processName,
            selector.processPath,
            selector.windowTitleMatch);

        if (window is null)
            return false;

        var windowRect = window.Current.BoundingRectangle;
        var x = (int)windowRect.Left + selector.relativeX;
        var y = (int)windowRect.Top + selector.relativeY;
        return TryClickPoint(x, y, windowRect, clickType);
    }

    private static bool TryScaledFallbackClick(DesktopSelector selector, string clickType)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0)
            return false;

        var union = screens[0].Bounds;
        foreach (var screen in screens.Skip(1))
            union = Rectangle.Union(union, screen.Bounds);

        var x = union.Left + (int)Math.Round(union.Width * selector.normalizedX);
        var y = union.Top + (int)Math.Round(union.Height * selector.normalizedY);
        return TryClickPoint(x, y, windowBounds: null, clickType);
    }

    private static bool TryAbsoluteFallbackClick(DesktopSelector selector, string clickType)
    {
        return TryClickPoint(selector.absoluteX, selector.absoluteY, windowBounds: null, clickType);
    }

    private static bool TryClickPoint(int x, int y, System.Windows.Rect? windowBounds, string clickType)
    {
        if (!IsPointSafe(x, y))
            return false;

        if (windowBounds.HasValue)
        {
            var bounds = windowBounds.Value;
            if (x < bounds.Left || y < bounds.Top || x > bounds.Right || y > bounds.Bottom)
                return false;
        }

        MouseSimulator.MoveTo(x, y);
        Thread.Sleep(100);
        if (string.Equals(clickType, "double", StringComparison.OrdinalIgnoreCase))
            MouseSimulator.DoubleClick();
        else
            MouseSimulator.Click();

        return true;
    }

    private static bool IsPointSafe(int x, int y)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            if (screen.Bounds.Contains(x, y))
                return true;
        }

        return false;
    }

    private static void TryRestoreWindow(DesktopSelector selector)
    {
        try
        {
            var window = AutomationElementLocator.FindElement(
                selector.windowTitle,
                automationId: "",
                name: "",
                controlType: "window",
                timeoutMs: 1000,
                processName: selector.processName,
                processPath: selector.processPath,
                titleMatch: selector.windowTitleMatch);

            if (window is null)
                return;

            var hwnd = window.Current.NativeWindowHandle;
            if (hwnd == 0)
                return;

            var ptr = new IntPtr(hwnd);
            WindowController.Restore(ptr);
            WindowController.Focus(ptr);
        }
        catch
        {
            // Fallback restoration is best effort only.
        }
    }
}
