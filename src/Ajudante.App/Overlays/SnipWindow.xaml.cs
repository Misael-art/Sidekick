using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ajudante.Platform.Screen;

namespace Ajudante.App.Overlays;

/// <summary>
/// Full-screen transparent overlay for the "Snip" (Screen Capture) mode.
/// The user draws a rectangle to select a screen region. On release the
/// region is captured as PNG bytes and the <see cref="RegionCaptured"/> event fires.
/// Press Escape or right-click to cancel.
/// </summary>
public partial class SnipWindow : Window
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    #endregion

    private bool _isDragging;
    private System.Windows.Point _dragStart;
    private int _virtualLeft;
    private int _virtualTop;
    private double _canvasWidth;
    private double _canvasHeight;

    /// <summary>
    /// Raised when the user completes a region selection.
    /// Provides the PNG bytes and the screen-coordinate rectangle.
    /// </summary>
    public event Action<byte[], System.Drawing.Rectangle>? RegionCaptured;

    public SnipWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Cover the entire virtual screen (all monitors)
        _virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        Left = _virtualLeft;
        Top = _virtualTop;
        Width = vw;
        Height = vh;
        WindowState = WindowState.Normal;

        _canvasWidth = vw;
        _canvasHeight = vh;
        OverlayCanvas.Width = vw;
        OverlayCanvas.Height = vh;

        // Position the full backdrop
        Canvas.SetLeft(Backdrop, 0);
        Canvas.SetTop(Backdrop, 0);
        Backdrop.Width = vw;
        Backdrop.Height = vh;

        // Hide the shade rectangles until dragging starts
        TopShade.Visibility = Visibility.Collapsed;
        BottomShade.Visibility = Visibility.Collapsed;
        LeftShade.Visibility = Visibility.Collapsed;
        RightShade.Visibility = Visibility.Collapsed;

        // Position hint panel at top-center
        HintPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double hintWidth = HintPanel.DesiredSize.Width;
        Canvas.SetLeft(HintPanel, (vw - hintWidth) / 2);
        Canvas.SetTop(HintPanel, 40);

        // Show crosshairs
        CrosshairH.Visibility = Visibility.Visible;
        CrosshairV.Visibility = Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(OverlayCanvas);

        // Switch from full backdrop to the four-shade approach
        Backdrop.Visibility = Visibility.Collapsed;
        TopShade.Visibility = Visibility.Visible;
        BottomShade.Visibility = Visibility.Visible;
        LeftShade.Visibility = Visibility.Visible;
        RightShade.Visibility = Visibility.Visible;

        SelectionBorder.Visibility = Visibility.Visible;
        SelectionDash.Visibility = Visibility.Visible;
        DimensionPanel.Visibility = Visibility.Visible;
        HintPanel.Visibility = Visibility.Collapsed;

        // Hide crosshairs during drag
        CrosshairH.Visibility = Visibility.Collapsed;
        CrosshairV.Visibility = Visibility.Collapsed;

        CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);

        if (!_isDragging)
        {
            // Update crosshair lines
            CrosshairH.X1 = 0;
            CrosshairH.X2 = _canvasWidth;
            CrosshairH.Y1 = pos.Y;
            CrosshairH.Y2 = pos.Y;

            CrosshairV.X1 = pos.X;
            CrosshairV.X2 = pos.X;
            CrosshairV.Y1 = 0;
            CrosshairV.Y2 = _canvasHeight;
            return;
        }

        UpdateSelection(pos);
    }

    private void UpdateSelection(System.Windows.Point currentPos)
    {
        // Calculate the selection rectangle
        double x = Math.Min(_dragStart.X, currentPos.X);
        double y = Math.Min(_dragStart.Y, currentPos.Y);
        double w = Math.Abs(currentPos.X - _dragStart.X);
        double h = Math.Abs(currentPos.Y - _dragStart.Y);

        // Clamp to canvas
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > _canvasWidth) w = _canvasWidth - x;
        if (y + h > _canvasHeight) h = _canvasHeight - y;
        if (w < 0) w = 0;
        if (h < 0) h = 0;

        // Position the selection border
        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = w;
        SelectionBorder.Height = h;

        // Position the dashed rectangle (inset by the border thickness)
        Canvas.SetLeft(SelectionDash, x + 1);
        Canvas.SetTop(SelectionDash, y + 1);
        SelectionDash.Width = Math.Max(0, w - 2);
        SelectionDash.Height = Math.Max(0, h - 2);

        // Update the four shade rectangles to create a "cutout"
        // Top shade: from top of canvas to top of selection, full width
        Canvas.SetLeft(TopShade, 0);
        Canvas.SetTop(TopShade, 0);
        TopShade.Width = _canvasWidth;
        TopShade.Height = y;

        // Bottom shade: from bottom of selection to bottom of canvas, full width
        Canvas.SetLeft(BottomShade, 0);
        Canvas.SetTop(BottomShade, y + h);
        BottomShade.Width = _canvasWidth;
        BottomShade.Height = Math.Max(0, _canvasHeight - (y + h));

        // Left shade: from left of canvas to left of selection, between top and bottom shades
        Canvas.SetLeft(LeftShade, 0);
        Canvas.SetTop(LeftShade, y);
        LeftShade.Width = x;
        LeftShade.Height = h;

        // Right shade: from right of selection to right of canvas, between top and bottom shades
        Canvas.SetLeft(RightShade, x + w);
        Canvas.SetTop(RightShade, y);
        RightShade.Width = Math.Max(0, _canvasWidth - (x + w));
        RightShade.Height = h;

        // Update dimension label
        int pixelW = (int)w;
        int pixelH = (int)h;
        DimensionText.Text = $"{pixelW} x {pixelH}";

        // Position dimension label below the selection, or above if near the bottom
        DimensionPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double labelWidth = DimensionPanel.DesiredSize.Width;
        double labelHeight = DimensionPanel.DesiredSize.Height;

        double labelX = x + (w - labelWidth) / 2;
        double labelY = y + h + 8;

        if (labelY + labelHeight > _canvasHeight - 4)
            labelY = y - labelHeight - 8;
        if (labelX < 4) labelX = 4;
        if (labelX + labelWidth > _canvasWidth - 4)
            labelX = _canvasWidth - labelWidth - 4;

        Canvas.SetLeft(DimensionPanel, labelX);
        Canvas.SetTop(DimensionPanel, labelY);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        ReleaseMouseCapture();

        var endPos = e.GetPosition(OverlayCanvas);

        // Calculate the screen-coordinate rectangle
        int sx = (int)Math.Min(_dragStart.X, endPos.X);
        int sy = (int)Math.Min(_dragStart.Y, endPos.Y);
        int sw = (int)Math.Abs(endPos.X - _dragStart.X);
        int sh = (int)Math.Abs(endPos.Y - _dragStart.Y);

        // Minimum selection size to avoid accidental clicks
        if (sw < 3 || sh < 3)
        {
            ResetToInitialState();
            return;
        }

        // Convert canvas coordinates to screen coordinates
        int screenX = sx + _virtualLeft;
        int screenY = sy + _virtualTop;
        var screenRect = new System.Drawing.Rectangle(screenX, screenY, sw, sh);

        // Hide the overlay before capturing so it does not appear in the screenshot
        Hide();

        // Small delay to allow the overlay to fully disappear
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                byte[] pngBytes = CaptureRegionAsPng(screenRect);
                Close();
                RegionCaptured?.Invoke(pngBytes, screenRect);
            }
            catch
            {
                Close();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static byte[] CaptureRegionAsPng(System.Drawing.Rectangle rect)
    {
        using var bitmap = ScreenCapture.CaptureRegion(rect.X, rect.Y, rect.Width, rect.Height);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private void ResetToInitialState()
    {
        // Return to the initial "full backdrop" state
        Backdrop.Visibility = Visibility.Visible;
        TopShade.Visibility = Visibility.Collapsed;
        BottomShade.Visibility = Visibility.Collapsed;
        LeftShade.Visibility = Visibility.Collapsed;
        RightShade.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        SelectionDash.Visibility = Visibility.Collapsed;
        DimensionPanel.Visibility = Visibility.Collapsed;
        HintPanel.Visibility = Visibility.Visible;
        CrosshairH.Visibility = Visibility.Visible;
        CrosshairV.Visibility = Visibility.Visible;
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Cancel();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
    }

    private void Cancel()
    {
        _isDragging = false;
        if (IsMouseCaptured)
            ReleaseMouseCapture();
        Close();
    }
}
