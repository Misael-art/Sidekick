using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Ajudante.Platform.Screen;

/// <summary>
/// Provides screen, region, and window capture using GDI BitBlt.
/// </summary>
public static class ScreenCapture
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDPIAware();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint SRCCOPY = 0x00CC0020;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    // PrintWindow flags
    private const uint PW_CLIENTONLY = 0x01;
    private const uint PW_RENDERFULLCONTENT = 0x02;

    #endregion

    static ScreenCapture()
    {
        // Ensure the process is DPI-aware so captured coordinates match the actual screen.
        try { SetProcessDPIAware(); }
        catch { /* may fail if already set via manifest */ }
    }

    /// <summary>
    /// Captures the entire virtual screen (all monitors).
    /// </summary>
    public static Bitmap CaptureScreen()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        return CaptureRegion(x, y, width, height);
    }

    /// <summary>
    /// Captures a rectangular region of the screen.
    /// </summary>
    public static Bitmap CaptureRegion(int x, int y, int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        IntPtr desktopHwnd = GetDesktopWindow();
        IntPtr desktopDC = GetDC(desktopHwnd);
        IntPtr memDC = CreateCompatibleDC(desktopDC);
        IntPtr hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        try
        {
            BitBlt(memDC, 0, 0, width, height, desktopDC, x, y, SRCCOPY);

            Bitmap result = Image.FromHbitmap(hBitmap);
            return result;
        }
        finally
        {
            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(desktopHwnd, desktopDC);
        }
    }

    /// <summary>
    /// Captures the contents of a specific window by its handle.
    /// Uses PrintWindow with PW_RENDERFULLCONTENT for DWM/composited windows,
    /// falling back to BitBlt if PrintWindow fails.
    /// </summary>
    public static Bitmap CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Window handle must not be zero.", nameof(hwnd));

        if (!GetWindowRect(hwnd, out RECT rect))
            throw new InvalidOperationException("Failed to get window rect. The window may no longer exist.");

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Window has zero or negative dimensions.");

        // Try PrintWindow first (works for off-screen / occluded windows)
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = g.GetHdc();
            bool printed = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
            g.ReleaseHdc(hdc);

            if (printed)
                return bitmap;
        }

        // Fallback: BitBlt from the window DC
        bitmap.Dispose();

        IntPtr windowDC = GetDC(hwnd);
        IntPtr memDC = CreateCompatibleDC(windowDC);
        IntPtr hBitmap = CreateCompatibleBitmap(windowDC, width, height);
        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        try
        {
            BitBlt(memDC, 0, 0, width, height, windowDC, 0, 0, SRCCOPY);
            return Image.FromHbitmap(hBitmap);
        }
        finally
        {
            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(hwnd, windowDC);
        }
    }
}
