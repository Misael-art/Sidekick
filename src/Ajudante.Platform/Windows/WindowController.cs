using System.Runtime.InteropServices;

namespace Ajudante.Platform.Windows;

public static class WindowController
{
    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    public static bool Focus(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        ShowWindow(hwnd, SW_RESTORE);
        BringWindowToTop(hwnd);
        return SetForegroundWindow(hwnd);
    }

    public static bool BringToFront(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        BringWindowToTop(hwnd);
        return SetForegroundWindow(hwnd);
    }

    public static bool Minimize(IntPtr hwnd) => hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_SHOWMINIMIZED);

    public static bool Maximize(IntPtr hwnd) => hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_SHOWMAXIMIZED);

    public static bool Restore(IntPtr hwnd) => hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_RESTORE);

    public static bool Hide(IntPtr hwnd) => hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_HIDE);

    public static bool ShowNormal(IntPtr hwnd) => hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_SHOWNORMAL);
}
