using System.Drawing;
using System.Runtime.InteropServices;

namespace Ajudante.Platform.Screen;

/// <summary>
/// Reads individual pixel colours from the screen using GDI.
/// </summary>
public static class PixelReader
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    private const uint CLR_INVALID = 0xFFFFFFFF;

    #endregion

    /// <summary>
    /// Gets the colour of the pixel at the specified screen coordinates.
    /// </summary>
    /// <param name="x">Screen X coordinate.</param>
    /// <param name="y">Screen Y coordinate.</param>
    /// <returns>The colour at the specified position.</returns>
    public static Color GetPixelColor(int x, int y)
    {
        IntPtr hdc = GetDC(IntPtr.Zero); // DC for the entire screen
        try
        {
            uint colorRef = GetPixel(hdc, x, y);
            if (colorRef == CLR_INVALID)
                throw new InvalidOperationException($"GetPixel failed at ({x}, {y}). The coordinates may be out of range.");

            // COLORREF is 0x00BBGGRR
            int r = (int)(colorRef & 0xFF);
            int g = (int)((colorRef >> 8) & 0xFF);
            int b = (int)((colorRef >> 16) & 0xFF);

            return Color.FromArgb(255, r, g, b);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <summary>
    /// Waits until the colour at the specified screen coordinates changes from its
    /// current value. Polls at ~50 ms intervals.
    /// </summary>
    /// <param name="x">Screen X coordinate.</param>
    /// <param name="y">Screen Y coordinate.</param>
    /// <param name="ct">Cancellation token to stop waiting.</param>
    /// <returns>The new colour after the change.</returns>
    public static async Task<Color> WaitForColorChange(int x, int y, CancellationToken ct = default)
    {
        Color initialColor = GetPixelColor(x, y);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(50, ct).ConfigureAwait(false);

            Color current = GetPixelColor(x, y);
            if (current != initialColor)
                return current;
        }

        ct.ThrowIfCancellationRequested();
        return default; // unreachable
    }
}
