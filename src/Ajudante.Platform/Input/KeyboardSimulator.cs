using System.Runtime.InteropServices;

namespace Ajudante.Platform.Input;

/// <summary>
/// Common virtual key codes for use with the keyboard simulator.
/// </summary>
public enum VirtualKey : ushort
{
    VK_BACK = 0x08,
    VK_TAB = 0x09,
    VK_RETURN = 0x0D,
    VK_SHIFT = 0x10,
    VK_CONTROL = 0x11,
    VK_MENU = 0x12,      // Alt key
    VK_PAUSE = 0x13,
    VK_CAPITAL = 0x14,    // Caps Lock
    VK_ESCAPE = 0x1B,
    VK_SPACE = 0x20,
    VK_PRIOR = 0x21,      // Page Up
    VK_NEXT = 0x22,       // Page Down
    VK_END = 0x23,
    VK_HOME = 0x24,
    VK_LEFT = 0x25,
    VK_UP = 0x26,
    VK_RIGHT = 0x27,
    VK_DOWN = 0x28,
    VK_PRINT = 0x2A,
    VK_SNAPSHOT = 0x2C,   // Print Screen
    VK_INSERT = 0x2D,
    VK_DELETE = 0x2E,

    // Digits
    VK_0 = 0x30,
    VK_1 = 0x31,
    VK_2 = 0x32,
    VK_3 = 0x33,
    VK_4 = 0x34,
    VK_5 = 0x35,
    VK_6 = 0x36,
    VK_7 = 0x37,
    VK_8 = 0x38,
    VK_9 = 0x39,

    // Letters
    VK_A = 0x41,
    VK_B = 0x42,
    VK_C = 0x43,
    VK_D = 0x44,
    VK_E = 0x45,
    VK_F = 0x46,
    VK_G = 0x47,
    VK_H = 0x48,
    VK_I = 0x49,
    VK_J = 0x4A,
    VK_K = 0x4B,
    VK_L = 0x4C,
    VK_M = 0x4D,
    VK_N = 0x4E,
    VK_O = 0x4F,
    VK_P = 0x50,
    VK_Q = 0x51,
    VK_R = 0x52,
    VK_S = 0x53,
    VK_T = 0x54,
    VK_U = 0x55,
    VK_V = 0x56,
    VK_W = 0x57,
    VK_X = 0x58,
    VK_Y = 0x59,
    VK_Z = 0x5A,

    VK_LWIN = 0x5B,
    VK_RWIN = 0x5C,

    // Numpad
    VK_NUMPAD0 = 0x60,
    VK_NUMPAD1 = 0x61,
    VK_NUMPAD2 = 0x62,
    VK_NUMPAD3 = 0x63,
    VK_NUMPAD4 = 0x64,
    VK_NUMPAD5 = 0x65,
    VK_NUMPAD6 = 0x66,
    VK_NUMPAD7 = 0x67,
    VK_NUMPAD8 = 0x68,
    VK_NUMPAD9 = 0x69,
    VK_MULTIPLY = 0x6A,
    VK_ADD = 0x6B,
    VK_SUBTRACT = 0x6D,
    VK_DECIMAL = 0x6E,
    VK_DIVIDE = 0x6F,

    // Function keys
    VK_F1 = 0x70,
    VK_F2 = 0x71,
    VK_F3 = 0x72,
    VK_F4 = 0x73,
    VK_F5 = 0x74,
    VK_F6 = 0x75,
    VK_F7 = 0x76,
    VK_F8 = 0x77,
    VK_F9 = 0x78,
    VK_F10 = 0x79,
    VK_F11 = 0x7A,
    VK_F12 = 0x7B,

    VK_NUMLOCK = 0x90,
    VK_SCROLL = 0x91,

    VK_LSHIFT = 0xA0,
    VK_RSHIFT = 0xA1,
    VK_LCONTROL = 0xA2,
    VK_RCONTROL = 0xA3,
    VK_LMENU = 0xA4,     // Left Alt
    VK_RMENU = 0xA5,     // Right Alt

    VK_OEM_1 = 0xBA,      // ;:
    VK_OEM_PLUS = 0xBB,   // =+
    VK_OEM_COMMA = 0xBC,  // ,<
    VK_OEM_MINUS = 0xBD,  // -_
    VK_OEM_PERIOD = 0xBE, // .>
    VK_OEM_2 = 0xBF,      // /?
    VK_OEM_3 = 0xC0,      // `~
    VK_OEM_4 = 0xDB,      // [{
    VK_OEM_5 = 0xDC,      // \|
    VK_OEM_6 = 0xDD,      // ]}
    VK_OEM_7 = 0xDE,      // '"
}

public static class KeyboardSimulator
{
    #region Win32 Interop

    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

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

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_UNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUT_UNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint MAPVK_VK_TO_VSC = 0;

    // Extended keys that require the KEYEVENTF_EXTENDEDKEY flag
    private static readonly HashSet<VirtualKey> ExtendedKeys =
    [
        VirtualKey.VK_INSERT, VirtualKey.VK_DELETE, VirtualKey.VK_HOME, VirtualKey.VK_END,
        VirtualKey.VK_PRIOR, VirtualKey.VK_NEXT,
        VirtualKey.VK_LEFT, VirtualKey.VK_UP, VirtualKey.VK_RIGHT, VirtualKey.VK_DOWN,
        VirtualKey.VK_NUMLOCK, VirtualKey.VK_SNAPSHOT,
        VirtualKey.VK_DIVIDE,
        VirtualKey.VK_RCONTROL, VirtualKey.VK_RMENU,
        VirtualKey.VK_LWIN, VirtualKey.VK_RWIN,
    ];

    #endregion

    /// <summary>
    /// Types a string of text using Unicode input events. Handles any character
    /// including non-ASCII, emoji, and special symbols.
    /// </summary>
    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<INPUT>();

        foreach (char c in text)
        {
            // Key down (Unicode)
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE
                    }
                }
            });

            // Key up (Unicode)
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                    }
                }
            });
        }

        var inputArray = inputs.ToArray();
        SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Presses and releases a single virtual key.
    /// </summary>
    public static void PressKey(VirtualKey key)
    {
        KeyDown(key);
        KeyUp(key);
    }

    /// <summary>
    /// Presses (holds down) a virtual key.
    /// </summary>
    public static void KeyDown(VirtualKey key)
    {
        uint flags = KEYEVENTF_KEYDOWN;
        if (ExtendedKeys.Contains(key))
            flags |= KEYEVENTF_EXTENDEDKEY;

        ushort scanCode = (ushort)MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = scanCode,
                    dwFlags = flags
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Releases a virtual key.
    /// </summary>
    public static void KeyUp(VirtualKey key)
    {
        uint flags = KEYEVENTF_KEYUP;
        if (ExtendedKeys.Contains(key))
            flags |= KEYEVENTF_EXTENDEDKEY;

        ushort scanCode = (ushort)MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = scanCode,
                    dwFlags = flags
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Presses a combination of keys simultaneously (e.g., Ctrl+Shift+S).
    /// Modifier keys are pressed in order, then the last key is pressed and released,
    /// then modifiers are released in reverse order.
    /// </summary>
    public static void PressCombo(params VirtualKey[] keys)
    {
        if (keys.Length == 0) return;

        // Press all modifier keys (all keys except the last one)
        for (int i = 0; i < keys.Length - 1; i++)
        {
            KeyDown(keys[i]);
        }

        // Press and release the final key
        PressKey(keys[^1]);

        // Release modifier keys in reverse order
        for (int i = keys.Length - 2; i >= 0; i--)
        {
            KeyUp(keys[i]);
        }
    }
}
