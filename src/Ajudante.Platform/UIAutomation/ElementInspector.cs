using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using Ajudante.Platform.Screen;

namespace Ajudante.Platform.UIAutomation;

/// <summary>
/// Inspects UI elements using the Windows UI Automation framework.
/// </summary>
public static class ElementInspector
{
    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    #endregion

    /// <summary>
    /// Gets information about the UI element at the specified screen coordinates.
    /// </summary>
    public static ElementInfo? GetElementAtPoint(int x, int y)
    {
        try
        {
            var point = new System.Windows.Point(x, y);
            AutomationElement? element = AutomationElement.FromPoint(point);
            return element is null ? null : BuildElementInfo(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets information about the UI element currently under the mouse cursor.
    /// </summary>
    public static ElementInfo? GetElementUnderCursor()
    {
        if (!GetCursorPos(out POINT pt))
            return null;

        return GetElementAtPoint(pt.X, pt.Y);
    }

    /// <summary>
    /// Enumerates all direct child UI elements of the specified window.
    /// </summary>
    public static List<ElementInfo> GetWindowElements(IntPtr hwnd)
    {
        var results = new List<ElementInfo>();

        if (hwnd == IntPtr.Zero)
            return results;

        try
        {
            AutomationElement? root = AutomationElement.FromHandle(hwnd);
            if (root is null) return results;

            CollectDescendants(root, results, maxDepth: 5, currentDepth: 0);
        }
        catch (ElementNotAvailableException)
        {
            // Window may have been closed
        }

        return results;
    }

    /// <summary>
    /// Recursively collects element info from the automation tree, bounded by depth.
    /// </summary>
    private static void CollectDescendants(AutomationElement parent, List<ElementInfo> results, int maxDepth, int currentDepth)
    {
        if (currentDepth > maxDepth) return;

        AutomationElement? child = TreeWalker.ControlViewWalker.GetFirstChild(parent);
        while (child is not null)
        {
            try
            {
                results.Add(BuildElementInfo(child));
                CollectDescendants(child, results, maxDepth, currentDepth + 1);
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
            catch (ElementNotAvailableException)
            {
                // Element disappeared; skip and try the next sibling
                try { child = TreeWalker.ControlViewWalker.GetNextSibling(child); }
                catch { break; }
            }
        }
    }

    /// <summary>
    /// Builds an ElementInfo from an AutomationElement, reading all relevant properties.
    /// </summary>
    private static ElementInfo BuildElementInfo(AutomationElement element)
    {
        string automationId = SafeGetProperty(element, AutomationElement.AutomationIdProperty) ?? "";
        string name = SafeGetProperty(element, AutomationElement.NameProperty) ?? "";
        string className = SafeGetProperty(element, AutomationElement.ClassNameProperty) ?? "";
        int processId = SafeGetIntProperty(element, AutomationElement.ProcessIdProperty);

        string controlType = "";
        try
        {
            var ct = element.Current.ControlType;
            controlType = ct?.ProgrammaticName?.Replace("ControlType.", "") ?? "";
        }
        catch { /* unavailable */ }

        Rectangle boundingRect = Rectangle.Empty;
        try
        {
            var rect = element.Current.BoundingRectangle;
            if (!rect.IsEmpty && !double.IsInfinity(rect.Width) && !double.IsInfinity(rect.Height))
            {
                boundingRect = new Rectangle(
                    (int)rect.X, (int)rect.Y,
                    (int)rect.Width, (int)rect.Height);
            }
        }
        catch { /* unavailable */ }

        var (windowTitle, windowBounds) = FindWindowMetadata(element);
        var windowHandle = FindWindowHandle(element);
        var (processName, processPath) = GetProcessMetadata(processId);
        var cursor = GetCursorPos(out POINT pt) ? new Point(pt.X, pt.Y) : Point.Empty;
        var cursorPixel = "";
        if (!cursor.IsEmpty)
        {
            try
            {
                var color = PixelReader.GetPixelColor(cursor.X, cursor.Y);
                cursorPixel = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            catch
            {
                cursorPixel = "";
            }
        }

        var monitor = cursor.IsEmpty ? global::System.Windows.Forms.Screen.PrimaryScreen : global::System.Windows.Forms.Screen.FromPoint(cursor);
        var monitorBounds = monitor?.Bounds ?? Rectangle.Empty;
        var monitorName = monitor?.DeviceName ?? "";
        var hostBounds = SystemInformation.VirtualScreen;
        var relativePointX = cursor.X - windowBounds.X;
        var relativePointY = cursor.Y - windowBounds.Y;
        var normalizedWindowX = windowBounds.Width > 0 ? (double)relativePointX / windowBounds.Width : 0d;
        var normalizedWindowY = windowBounds.Height > 0 ? (double)relativePointY / windowBounds.Height : 0d;
        var normalizedScreenX = hostBounds.Width > 0 ? (double)(cursor.X - hostBounds.X) / hostBounds.Width : 0d;
        var normalizedScreenY = hostBounds.Height > 0 ? (double)(cursor.Y - hostBounds.Y) / hostBounds.Height : 0d;
        var windowState = ResolveWindowState(windowHandle);

        return new ElementInfo
        {
            AutomationId = automationId,
            Name = name,
            ClassName = className,
            ControlType = controlType,
            BoundingRect = boundingRect,
            WindowBounds = windowBounds,
            ProcessId = processId,
            WindowTitle = windowTitle,
            WindowStateAtCapture = windowState,
            WindowHandle = windowHandle.ToInt64(),
            ProcessName = processName,
            ProcessPath = processPath,
            CursorScreen = cursor,
            CursorPixelColor = cursorPixel,
            IsFocused = SafeGetBoolProperty(element, AutomationElement.HasKeyboardFocusProperty),
            IsEnabled = SafeGetBoolProperty(element, AutomationElement.IsEnabledProperty),
            IsOffscreen = SafeGetBoolProperty(element, AutomationElement.IsOffscreenProperty),
            MonitorDeviceName = monitorName,
            MonitorBounds = monitorBounds,
            HostScreenWidth = hostBounds.Width,
            HostScreenHeight = hostBounds.Height,
            DpiScale = 1.0d,
            RelativePointX = relativePointX,
            RelativePointY = relativePointY,
            NormalizedWindowX = ClampNormalized(normalizedWindowX),
            NormalizedWindowY = ClampNormalized(normalizedWindowY),
            NormalizedScreenX = ClampNormalized(normalizedScreenX),
            NormalizedScreenY = ClampNormalized(normalizedScreenY)
        };
    }

    /// <summary>
    /// Walks up the automation tree to find the nearest Window element and returns its name and bounds.
    /// </summary>
    private static (string windowTitle, Rectangle windowBounds) FindWindowMetadata(AutomationElement element)
    {
        try
        {
            AutomationElement? current = element;
            while (current is not null)
            {
                try
                {
                    if (current.Current.ControlType == ControlType.Window)
                    {
                        var bounds = Rectangle.Empty;
                        try
                        {
                            var rect = current.Current.BoundingRectangle;
                            if (!rect.IsEmpty && !double.IsInfinity(rect.Width) && !double.IsInfinity(rect.Height))
                            {
                                bounds = new Rectangle(
                                    (int)rect.X,
                                    (int)rect.Y,
                                    (int)rect.Width,
                                    (int)rect.Height);
                            }
                        }
                        catch
                        {
                            // Bounds are optional diagnostics.
                        }

                        return (current.Current.Name ?? "", bounds);
                    }
                }
                catch { /* skip */ }

                current = TreeWalker.ControlViewWalker.GetParent(current);
            }
        }
        catch { /* unavailable */ }

        return ("", Rectangle.Empty);
    }

    private static IntPtr FindWindowHandle(AutomationElement element)
    {
        try
        {
            var current = element;
            while (current is not null)
            {
                try
                {
                    if (current.Current.ControlType == ControlType.Window)
                    {
                        var nativeHandle = current.Current.NativeWindowHandle;
                        return nativeHandle == 0 ? IntPtr.Zero : new IntPtr(nativeHandle);
                    }
                }
                catch
                {
                    // Keep searching upwards.
                }

                current = TreeWalker.ControlViewWalker.GetParent(current);
            }
        }
        catch
        {
            // no-op
        }

        return IntPtr.Zero;
    }

    private static string ResolveWindowState(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
            return "normal";

        try
        {
            return IsIconic(windowHandle) ? "minimized" : "normal";
        }
        catch
        {
            return "normal";
        }
    }

    private static (string processName, string processPath) GetProcessMetadata(int processId)
    {
        if (processId <= 0)
            return ("", "");

        try
        {
            using var process = Process.GetProcessById(processId);
            var processName = process.ProcessName ?? "";
            var processPath = "";
            try
            {
                processPath = process.MainModule?.FileName ?? "";
            }
            catch
            {
                // Access can be denied across integrity/session/bitness boundaries.
            }

            return (processName, processPath);
        }
        catch
        {
            return ("", "");
        }
    }

    private static string? SafeGetProperty(AutomationElement element, AutomationProperty property)
    {
        try
        {
            object? val = element.GetCurrentPropertyValue(property, true);
            return val == AutomationElement.NotSupported ? null : val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static int SafeGetIntProperty(AutomationElement element, AutomationProperty property)
    {
        try
        {
            object? val = element.GetCurrentPropertyValue(property, true);
            if (val == AutomationElement.NotSupported) return 0;
            return val is int i ? i : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool SafeGetBoolProperty(AutomationElement element, AutomationProperty property)
    {
        try
        {
            object? val = element.GetCurrentPropertyValue(property, true);
            return val is bool boolValue && boolValue;
        }
        catch
        {
            return false;
        }
    }

    private static double ClampNormalized(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0d;
        return Math.Max(0d, Math.Min(1d, value));
    }
}
