using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.App.Overlays;

/// <summary>
/// Full-screen transparent overlay for the "Mira" (Inspector) mode.
/// Highlights the UI element under the cursor and displays its properties.
/// Click to capture the element; press Escape to cancel.
/// </summary>
public partial class MiraWindow : Window
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
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    #endregion

    private readonly DispatcherTimer _pollTimer;
    private ElementInfo? _currentElement;
    private System.Drawing.Rectangle _lastBounds;

    /// <summary>
    /// Raised when the user clicks to capture a UI element.
    /// </summary>
    public event Action<ElementInfo>? ElementCaptured;

    public MiraWindow()
    {
        InitializeComponent();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _pollTimer.Tick += PollTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Cover the entire virtual screen (all monitors)
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        Left = vx;
        Top = vy;
        Width = vw;
        Height = vh;
        WindowState = WindowState.Normal;

        OverlayCanvas.Width = vw;
        OverlayCanvas.Height = vh;

        _pollTimer.Start();
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            ElementInfo? element = GetElementUnderCursorIgnoringOverlay();

            if (element is null)
            {
                HideHighlight();
                _currentElement = null;
                return;
            }

            _currentElement = element;
            var bounds = element.BoundingRect;

            // Only update visuals if the element changed
            if (bounds != _lastBounds)
            {
                _lastBounds = bounds;
                UpdateHighlight(bounds);
            }

            UpdateInfoPanel(element);
        }
        catch
        {
            // Swallow exceptions from UI Automation to keep the overlay stable
        }
    }

    private ElementInfo? GetElementUnderCursorIgnoringOverlay()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return ElementInspector.GetElementUnderCursor();
        }

        var originalStyle = GetWindowExStyle(hwnd);
        var transparentStyle = new IntPtr(originalStyle.ToInt64() | WS_EX_TRANSPARENT);

        try
        {
            SetWindowExStyle(hwnd, transparentStyle);
            return ElementInspector.GetElementUnderCursor();
        }
        finally
        {
            SetWindowExStyle(hwnd, originalStyle);
        }
    }

    private static IntPtr GetWindowExStyle(IntPtr hwnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE)
            : new IntPtr(GetWindowLong32(hwnd, GWL_EXSTYLE));
    }

    private static void SetWindowExStyle(IntPtr hwnd, IntPtr style)
    {
        if (IntPtr.Size == 8)
        {
            _ = SetWindowLongPtr64(hwnd, GWL_EXSTYLE, style);
        }
        else
        {
            _ = SetWindowLong32(hwnd, GWL_EXSTYLE, style.ToInt32());
        }
    }

    private void UpdateHighlight(System.Drawing.Rectangle bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            HideHighlight();
            return;
        }

        // Convert screen coordinates to canvas coordinates (offset by virtual screen origin)
        double canvasX = bounds.X - Left;
        double canvasY = bounds.Y - Top;

        Canvas.SetLeft(HighlightBorder, canvasX);
        Canvas.SetTop(HighlightBorder, canvasY);
        HighlightBorder.Width = bounds.Width;
        HighlightBorder.Height = bounds.Height;
        HighlightBorder.Visibility = Visibility.Visible;
    }

    private void UpdateInfoPanel(ElementInfo element)
    {
        ControlTypeText.Text = string.IsNullOrEmpty(element.ControlType)
            ? "(Unknown)"
            : element.ControlType;
        NameText.Text = string.IsNullOrEmpty(element.Name) ? "(none)" : element.Name;
        DetectedTextText.Text = string.IsNullOrEmpty(element.DetectedText) ? "(none)" : element.DetectedText;
        CurrentTextText.Text = string.IsNullOrEmpty(element.CurrentText) ? "(none)" : element.CurrentText;
        PlaceholderText.Text = string.IsNullOrEmpty(element.PlaceholderText) ? "(none)" : element.PlaceholderText;
        TextSourceText.Text = $"{element.TextSource} / {element.CaptureQuality}";
        OcrWarningText.Text = element.OcrWarning;
        OcrWarningText.Visibility = string.IsNullOrWhiteSpace(element.OcrWarning)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ClassNameText.Text = string.IsNullOrEmpty(element.ClassName) ? "(none)" : element.ClassName;
        AutomationIdText.Text = string.IsNullOrEmpty(element.AutomationId) ? "(none)" : element.AutomationId;
        BoundsText.Text = $"{element.BoundingRect.X}, {element.BoundingRect.Y} " +
                          $"({element.BoundingRect.Width} x {element.BoundingRect.Height})";
        RelativeBoundsText.Text = element.RelativeBoundingRect.IsEmpty
            ? "(none)"
            : $"{element.RelativeBoundingRect.X}, {element.RelativeBoundingRect.Y} " +
              $"({element.RelativeBoundingRect.Width} x {element.RelativeBoundingRect.Height})";
        WindowTitleText.Text = string.IsNullOrEmpty(element.WindowTitle) ? "(none)" : element.WindowTitle;
        ProcessText.Text = string.IsNullOrEmpty(element.ProcessName)
            ? $"PID {element.ProcessId}"
            : $"{element.ProcessName} (PID {element.ProcessId})";
        ProcessPathText.Text = string.IsNullOrEmpty(element.ProcessPath) ? "(unavailable)" : element.ProcessPath;
        CursorText.Text = $"{element.CursorScreen.X}, {element.CursorScreen.Y}"
            + (string.IsNullOrWhiteSpace(element.CursorPixelColor) ? "" : $"  {element.CursorPixelColor}");
        StateText.Text = $"focused={element.IsFocused} enabled={element.IsEnabled} visible={!element.IsOffscreen}";
        var strength = SelectorStrengthEvaluator.Evaluate(element);
        var strategy = SelectorStrengthEvaluator.SuggestStrategy(element);
        SelectorText.Text = $"{SelectorStrengthEvaluator.ToPublicLabel(strength)} / {SelectorStrengthEvaluator.ToPublicStrategy(strategy)}";

        // Position the info panel near the cursor, but keep it on screen
        if (!GetCursorPos(out POINT cursor))
            return;

        double panelX = cursor.X - Left + 16;
        double panelY = cursor.Y - Top + 20;

        // Measure desired size
        InfoPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double panelWidth = InfoPanel.DesiredSize.Width;
        double panelHeight = InfoPanel.DesiredSize.Height;

        // Keep within the canvas bounds
        if (panelX + panelWidth > Width - 8)
            panelX = cursor.X - Left - panelWidth - 8;
        if (panelY + panelHeight > Height - 8)
            panelY = cursor.Y - Top - panelHeight - 8;
        if (panelX < 4) panelX = 4;
        if (panelY < 4) panelY = 4;

        Canvas.SetLeft(InfoPanel, panelX);
        Canvas.SetTop(InfoPanel, panelY);
        InfoPanel.Visibility = Visibility.Visible;
    }

    private void HideHighlight()
    {
        HighlightBorder.Visibility = Visibility.Collapsed;
        InfoPanel.Visibility = Visibility.Collapsed;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_currentElement is not null)
        {
            _pollTimer.Stop();
            var captured = _currentElement;
            Close();
            ElementCaptured?.Invoke(captured);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _pollTimer.Stop();
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _pollTimer.Stop();
        base.OnClosed(e);
    }
}
