using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        string helpText = SafeGetProperty(element, AutomationElement.HelpTextProperty) ?? "";
        string valueText = TryReadValuePattern(element);
        string textPatternText = TryReadTextPattern(element);
        var (legacyName, legacyValue) = TryReadLegacyPattern(element);
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
        var ocr = TryReadOcrFallback(boundingRect);
        var text = ResolveTextFields(
            controlType,
            automationId,
            name,
            valueText,
            textPatternText,
            legacyName,
            legacyValue,
            helpText,
            ocr.Text,
            ocr.Attempted,
            ocr.Available);
        var browser = ResolveBrowserContext(
            processName,
            windowTitle,
            controlType,
            className,
            automationId,
            name,
            valueText,
            textPatternText,
            legacyName,
            legacyValue,
            helpText,
            text.DetectedText);

        return new ElementInfo
        {
            AutomationId = automationId,
            Name = name,
            ValueText = valueText,
            TextPatternText = textPatternText,
            LegacyName = legacyName,
            LegacyValue = legacyValue,
            HelpText = helpText,
            DetectedText = text.DetectedText,
            CurrentText = text.CurrentText,
            PlaceholderText = text.PlaceholderText,
            TextSource = text.TextSource,
            CaptureQuality = text.CaptureQuality,
            OcrAttempted = ocr.Attempted,
            OcrAvailable = ocr.Available,
            OcrText = ocr.Text,
            OcrWarning = ocr.Warning,
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
            NormalizedScreenY = ClampNormalized(normalizedScreenY),
            IsBrowserSurface = browser.IsBrowserSurface,
            BrowserUrl = browser.Url,
            BrowserOrigin = browser.Origin,
            BrowserDocumentTitle = browser.DocumentTitle,
            BrowserCaptureHint = browser.CaptureHint
        };
    }

    private static BrowserContext ResolveBrowserContext(
        string processName,
        string windowTitle,
        string controlType,
        string className,
        string automationId,
        string name,
        string valueText,
        string textPatternText,
        string legacyName,
        string legacyValue,
        string helpText,
        string detectedText)
    {
        var browserProcess = IsKnownBrowserProcess(processName);
        var browserSurface = browserProcess
            || string.Equals(controlType, "Document", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Chrome_RenderWidgetHost", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
            || string.Equals(automationId, "RootWebArea", StringComparison.OrdinalIgnoreCase);

        var url = ExtractUrl(valueText, textPatternText, legacyValue, legacyName, helpText, detectedText, name, windowTitle);
        var origin = ResolveOrigin(url);
        var documentTitle = FirstNonEmpty(
            CleanBrowserTitle(name, url),
            CleanBrowserTitle(detectedText, url),
            CleanBrowserTitle(windowTitle, url));

        var hint = browserSurface
            ? string.IsNullOrWhiteSpace(url)
                ? "Browser detectado via processo/UIAutomation; URL/DOM completo nao foi exposto. Use Browser nodes, seletor Mira e fallback relativo."
                : "Browser detectado; Mira salvou URL/origem expostas por UIAutomation. DOM interno completo ainda depende do navegador expor acessibilidade."
            : "";

        return new BrowserContext(browserSurface, url, origin, documentTitle, hint);
    }

    private static bool IsKnownBrowserProcess(string processName)
    {
        var normalized = processName.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.Equals("msedge", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("chrome", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("firefox", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("brave", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("opera", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("vivaldi", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractUrl(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = Regex.Match(
                candidate,
                @"\b((?:https?|devtools|edge|chrome|about|file)://[^\s""'<>]+|[a-z0-9.-]+\.[a-z]{2,}(?:/[^\s""'<>]*)?)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Groups[1].Value.TrimEnd('.', ',', ';', ')');
            }
        }

        return "";
    }

    private static string ResolveOrigin(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "";
        }

        var candidate = url.Contains("://", StringComparison.Ordinal) ? url : $"https://{url}";
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            ? uri.Host
            : "";
    }

    private static string CleanBrowserTitle(string value, string url)
    {
        var title = CleanText(value);
        if (string.IsNullOrWhiteSpace(title))
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            title = title.Replace(url, "", StringComparison.OrdinalIgnoreCase).Trim(' ', '-', '|', '\u2014');
        }

        return title.Length > 120 ? title[..120] : title;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static string TryReadValuePattern(AutomationElement element)
    {
        try
        {
            return element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern valuePattern
                ? valuePattern.Current.Value ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    private static string TryReadTextPattern(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
            {
                return CleanText(textPattern.DocumentRange.GetText(4096));
            }
        }
        catch
        {
            // TextPattern can throw when the element disappears while Mira is hovering.
        }

        return "";
    }

    private static (string name, string value) TryReadLegacyPattern(AutomationElement element)
    {
        try
        {
            var legacyPattern = AutomationPattern.LookupById(10018);
            if (legacyPattern is not null && element.TryGetCurrentPattern(legacyPattern, out var pattern))
            {
                var current = pattern.GetType().GetProperty("Current")?.GetValue(pattern);
                var name = current?.GetType().GetProperty("Name")?.GetValue(current)?.ToString() ?? "";
                var value = current?.GetType().GetProperty("Value")?.GetValue(current)?.ToString() ?? "";
                return (name, value);
            }
        }
        catch
        {
            // LegacyIAccessible is best-effort across process boundaries.
        }

        return ("", "");
    }

    private static (string Text, bool Attempted, bool Available, string Warning) TryReadOcrFallback(Rectangle boundingRect)
    {
        if (boundingRect.Width <= 0 || boundingRect.Height <= 0)
        {
            return ("", false, false, "");
        }

        return (
            "",
            true,
            false,
            "OCR local ainda nao esta empacotado; a captura tentou fallback visual e preservou seletor/coordenadas.");
    }

    private static (string DetectedText, string CurrentText, string PlaceholderText, string TextSource, string CaptureQuality) ResolveTextFields(
        string controlType,
        string automationId,
        string name,
        string valueText,
        string textPatternText,
        string legacyName,
        string legacyValue,
        string helpText,
        string ocrText,
        bool ocrAttempted,
        bool ocrAvailable)
    {
        var currentText = CleanText(valueText);
        var placeholderText = CleanText(helpText);
        var isTextInput = string.Equals(controlType, "Edit", StringComparison.OrdinalIgnoreCase)
            || controlType.Contains("text", StringComparison.OrdinalIgnoreCase)
            || controlType.Contains("search", StringComparison.OrdinalIgnoreCase);

        if (isTextInput && string.IsNullOrWhiteSpace(currentText) && string.IsNullOrWhiteSpace(placeholderText))
        {
            placeholderText = CleanText(name);
        }

        var candidates = new (string Source, string Text)[]
        {
            ("ValuePattern", currentText),
            ("TextPattern", CleanText(textPatternText)),
            ("Legacy", CleanText(legacyValue)),
            ("Legacy", CleanText(legacyName)),
            ("UIAutomation", CleanText(name)),
            ("HelpText", placeholderText),
            ("OCR", CleanText(ocrText))
        };

        var selected = candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Text));
        var detectedText = selected.Text ?? "";
        var source = string.IsNullOrWhiteSpace(detectedText)
            ? (ocrAttempted && !ocrAvailable ? "fallback" : "fallback")
            : selected.Source;

        var hasSelector = !string.IsNullOrWhiteSpace(automationId) || !string.IsNullOrWhiteSpace(name);
        var quality = hasSelector && !string.IsNullOrWhiteSpace(detectedText)
            ? "forte"
            : !string.IsNullOrWhiteSpace(detectedText) ? "media" : "fraca";

        return (detectedText, currentText, placeholderText, source, quality);
    }

    private static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
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

    private sealed record BrowserContext(
        bool IsBrowserSurface,
        string Url,
        string Origin,
        string DocumentTitle,
        string CaptureHint);
}
