using System.Runtime.InteropServices;

namespace Ajudante.Platform.Input;

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public static class MouseSimulator
{
    #region Win32 Interop

    private const uint INPUT_MOUSE = 0;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    private const int WHEEL_DELTA = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    #endregion

    /// <summary>
    /// Moves the mouse cursor to the specified screen coordinates.
    /// </summary>
    public static void MoveTo(int x, int y)
    {
        // Use SetCursorPos for reliable absolute positioning, then send a matching
        // MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE so applications that track input events see it.
        SetCursorPos(x, y);

        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        // Normalised coordinates: 0..65535 maps to the primary monitor.
        int normalizedX = (int)((x * 65535.0) / (screenWidth - 1));
        int normalizedY = (int)((y * 65535.0) / (screenHeight - 1));

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = normalizedX,
                dy = normalizedY,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Clicks the specified mouse button at the current cursor position.
    /// </summary>
    public static void Click(MouseButton button = MouseButton.Left)
    {
        var (downFlag, upFlag) = GetButtonFlags(button);

        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = downFlag }
            },
            new()
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = upFlag }
            }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Double-clicks the left mouse button at the current cursor position.
    /// </summary>
    public static void DoubleClick()
    {
        Click(MouseButton.Left);
        Click(MouseButton.Left);
    }

    /// <summary>
    /// Right-clicks at the current cursor position.
    /// </summary>
    public static void RightClick() => Click(MouseButton.Right);

    /// <summary>
    /// Middle-clicks at the current cursor position.
    /// </summary>
    public static void MiddleClick() => Click(MouseButton.Middle);

    /// <summary>
    /// Performs a drag operation from one point to another using the left mouse button.
    /// </summary>
    public static void DragTo(int fromX, int fromY, int toX, int toY)
    {
        MoveTo(fromX, fromY);
        Thread.Sleep(50);

        // Press left button down
        var downInput = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN }
        };
        SendInput(1, [downInput], Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);

        // Move in small steps for smooth dragging that applications can follow
        const int steps = 20;
        for (int i = 1; i <= steps; i++)
        {
            int currentX = fromX + (toX - fromX) * i / steps;
            int currentY = fromY + (toY - fromY) * i / steps;
            MoveTo(currentX, currentY);
            Thread.Sleep(10);
        }

        Thread.Sleep(50);

        // Release left button
        var upInput = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP }
        };
        SendInput(1, [upInput], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Scrolls the mouse wheel up by the specified number of clicks.
    /// </summary>
    public static void ScrollUp(int clicks = 1)
    {
        Scroll(clicks * WHEEL_DELTA);
    }

    /// <summary>
    /// Scrolls the mouse wheel down by the specified number of clicks.
    /// </summary>
    public static void ScrollDown(int clicks = 1)
    {
        Scroll(-clicks * WHEEL_DELTA);
    }

    private static void Scroll(int amount)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dwFlags = MOUSEEVENTF_WHEEL,
                mouseData = amount
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static (uint down, uint up) GetButtonFlags(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
    }
}
