using System.Drawing;

namespace Ajudante.Platform.UIAutomation;

/// <summary>
/// Represents information about a UI Automation element.
/// </summary>
public sealed class ElementInfo
{
    /// <summary>The automation ID of the element (may be empty).</summary>
    public string AutomationId { get; init; } = "";

    /// <summary>The name / text of the element.</summary>
    public string Name { get; init; } = "";

    /// <summary>The class name of the underlying control.</summary>
    public string ClassName { get; init; } = "";

    /// <summary>The control type (e.g. "Button", "Edit", "Window").</summary>
    public string ControlType { get; init; } = "";

    /// <summary>The bounding rectangle in screen coordinates.</summary>
    public Rectangle BoundingRect { get; init; }

    /// <summary>The process ID that owns this element.</summary>
    public int ProcessId { get; init; }

    /// <summary>The title of the top-level window that contains this element.</summary>
    public string WindowTitle { get; init; } = "";

    public override string ToString() =>
        $"[{ControlType}] Name=\"{Name}\" AutomationId=\"{AutomationId}\" Class=\"{ClassName}\" " +
        $"Bounds={BoundingRect} PID={ProcessId}";
}
