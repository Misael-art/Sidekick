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

    /// <summary>ValuePattern.Value when the control exposes an editable/current value.</summary>
    public string ValueText { get; init; } = "";

    /// <summary>TextPattern range text when the control exposes visible text content.</summary>
    public string TextPatternText { get; init; } = "";

    /// <summary>LegacyIAccessible.Name when available.</summary>
    public string LegacyName { get; init; } = "";

    /// <summary>LegacyIAccessible.Value when available.</summary>
    public string LegacyValue { get; init; } = "";

    /// <summary>HelpText from UIAutomation, commonly used as hint/placeholder text.</summary>
    public string HelpText { get; init; } = "";

    /// <summary>Best user-visible text selected from Name, ValuePattern, TextPattern, legacy, OCR, or fallback.</summary>
    public string DetectedText { get; init; } = "";

    /// <summary>Current entered value for inputs when available.</summary>
    public string CurrentText { get; init; } = "";

    /// <summary>Placeholder/hint text for inputs when Windows exposes it.</summary>
    public string PlaceholderText { get; init; } = "";

    /// <summary>Source used to resolve DetectedText.</summary>
    public string TextSource { get; init; } = "fallback";

    /// <summary>Capture quality: forte, media, or fraca.</summary>
    public string CaptureQuality { get; init; } = "fraca";

    /// <summary>Whether an OCR fallback was attempted.</summary>
    public bool OcrAttempted { get; init; }

    /// <summary>Whether a local OCR engine was available.</summary>
    public bool OcrAvailable { get; init; }

    /// <summary>Text returned by OCR fallback when available.</summary>
    public string OcrText { get; init; } = "";

    /// <summary>Warning shown when OCR/fallback quality is limited.</summary>
    public string OcrWarning { get; init; } = "";

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

    /// <summary>Window state at capture time: normal, maximized, or minimized.</summary>
    public string WindowStateAtCapture { get; init; } = "normal";

    /// <summary>Native window handle when available.</summary>
    public long WindowHandle { get; init; }

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

    /// <summary>Monitor device name at capture time when available.</summary>
    public string MonitorDeviceName { get; init; } = "";

    /// <summary>Monitor bounds at capture time.</summary>
    public Rectangle MonitorBounds { get; init; }

    /// <summary>Host screen width at capture time.</summary>
    public int HostScreenWidth { get; init; }

    /// <summary>Host screen height at capture time.</summary>
    public int HostScreenHeight { get; init; }

    /// <summary>DPI scale at capture time when available.</summary>
    public double DpiScale { get; init; } = 1.0;

    /// <summary>Point relative to window top-left at capture time.</summary>
    public int RelativePointX { get; init; }

    /// <summary>Point relative to window top-left at capture time.</summary>
    public int RelativePointY { get; init; }

    /// <summary>Normalized horizontal point in window bounds (0..1).</summary>
    public double NormalizedWindowX { get; init; }

    /// <summary>Normalized vertical point in window bounds (0..1).</summary>
    public double NormalizedWindowY { get; init; }

    /// <summary>Normalized horizontal point in virtual desktop bounds (0..1).</summary>
    public double NormalizedScreenX { get; init; }

    /// <summary>Normalized vertical point in virtual desktop bounds (0..1).</summary>
    public double NormalizedScreenY { get; init; }

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
        $"[{ControlType}] Text=\"{DetectedText}\" Name=\"{Name}\" AutomationId=\"{AutomationId}\" Class=\"{ClassName}\" " +
        $"Bounds={BoundingRect} PID={ProcessId} Process=\"{ProcessName}\"";
}
