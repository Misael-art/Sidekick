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

    /// <summary>The bounding rectangle of the top-level window in screen coordinates.</summary>
    public Rectangle WindowBounds { get; init; }

    /// <summary>The process ID that owns this element.</summary>
    public int ProcessId { get; init; }

    /// <summary>The title of the top-level window that contains this element.</summary>
    public string WindowTitle { get; init; } = "";

    /// <summary>The owning process name without forcing an .exe suffix.</summary>
    public string ProcessName { get; init; } = "";

    /// <summary>The full executable path of the owning process when Windows permits reading it.</summary>
    public string ProcessPath { get; init; } = "";

    /// <summary>Cursor position in screen coordinates at capture time.</summary>
    public Point CursorScreen { get; init; }

    /// <summary>Pixel color under the cursor at capture time, formatted as #RRGGBB.</summary>
    public string CursorPixelColor { get; init; } = "";

    /// <summary>Whether UIAutomation reports keyboard focus on the element.</summary>
    public bool IsFocused { get; init; }

    /// <summary>Whether UIAutomation reports the element as enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Whether UIAutomation reports the element as off-screen.</summary>
    public bool IsOffscreen { get; init; }

    /// <summary>Element bounds relative to the containing top-level window.</summary>
    public Rectangle RelativeBoundingRect
    {
        get
        {
            if (BoundingRect.IsEmpty || WindowBounds.IsEmpty)
                return Rectangle.Empty;

            return new Rectangle(
                BoundingRect.X - WindowBounds.X,
                BoundingRect.Y - WindowBounds.Y,
                BoundingRect.Width,
                BoundingRect.Height);
        }
    }

    public override string ToString() =>
        $"[{ControlType}] Name=\"{Name}\" AutomationId=\"{AutomationId}\" Class=\"{ClassName}\" " +
        $"Bounds={BoundingRect} PID={ProcessId} Process=\"{ProcessName}\"";
}
