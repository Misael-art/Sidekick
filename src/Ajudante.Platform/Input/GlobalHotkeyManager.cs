using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Ajudante.Platform.Input;

/// <summary>
/// Modifier flags for global hotkeys. Matches Win32 MOD_* constants.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Ctrl = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

/// <summary>
/// Manages system-wide hotkeys using a hidden message-only window.
/// Thread-safe: hotkeys can be registered and unregistered from any thread.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    #region Win32 Interop

    private const int WM_HOTKEY = 0x0312;
    private const int WM_DESTROY = 0x0002;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, IntPtr lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    #endregion

    private const uint WM_USER_REGISTER = 0x0400 + 1;
    private const uint WM_USER_UNREGISTER = 0x0400 + 2;
    private const uint WM_USER_SHUTDOWN = 0x0400 + 3;

    private readonly Thread _messageThread;
    private readonly ManualResetEventSlim _windowReady = new(false);
    private readonly ConcurrentDictionary<int, Action> _callbacks = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingRegistrations = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingUnregistrations = new();

    private IntPtr _hwnd;
    private WndProcDelegate? _wndProcDelegate; // prevent GC collection of delegate
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeyManager()
    {
        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "GlobalHotkeyManager"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        _windowReady.Wait();
    }

    /// <summary>
    /// Registers a global hotkey. Returns an integer ID that can be used to unregister it.
    /// Safe to call from any thread.
    /// </summary>
    public int RegisterHotkey(HotkeyModifiers modifiers, VirtualKey key, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int id = Interlocked.Increment(ref _nextId);
        _callbacks[id] = callback;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRegistrations[id] = tcs;

        // Encode modifiers and key into lParam: low word = modifiers, high word = key
        IntPtr lParam = (IntPtr)(((uint)key << 16) | (uint)modifiers);
        PostMessage(_hwnd, WM_USER_REGISTER, (IntPtr)id, lParam);

        // Block until registration completes on the message thread
        bool success = tcs.Task.GetAwaiter().GetResult();
        if (!success)
        {
            _callbacks.TryRemove(id, out _);
            throw new InvalidOperationException(
                $"Failed to register hotkey (modifiers={modifiers}, key={key}). " +
                $"Win32 error: {Marshal.GetLastWin32Error()}");
        }

        return id;
    }

    /// <summary>
    /// Unregisters a previously registered hotkey by its ID.
    /// Safe to call from any thread.
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingUnregistrations[id] = tcs;

        PostMessage(_hwnd, WM_USER_UNREGISTER, (IntPtr)id, IntPtr.Zero);

        tcs.Task.GetAwaiter().GetResult();
        _callbacks.TryRemove(id, out _);
    }

    /// <summary>
    /// Unregisters all currently registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (int id in _callbacks.Keys)
        {
            try { UnregisterHotkey(id); }
            catch { /* best effort */ }
        }
    }

    private void MessageLoop()
    {
        _wndProcDelegate = WndProc;
        IntPtr hInstance = GetModuleHandle(null);
        string className = $"AjudanteHotkeyWnd_{Environment.ProcessId}_{Environment.CurrentManagedThreadId}";
        IntPtr classNamePtr = Marshal.StringToHGlobalUni(className);

        try
        {
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = hInstance,
                lpszClassName = classNamePtr
            };

            ushort atom = RegisterClassEx(ref wc);
            if (atom == 0)
                throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

            _hwnd = CreateWindowEx(
                0, (IntPtr)atom, null, 0,
                0, 0, 0, 0,
                HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

            _windowReady.Set();

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_USER_SHUTDOWN)
                    break;

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY:
            {
                int id = wParam.ToInt32();
                if (_callbacks.TryGetValue(id, out var callback))
                {
                    // Fire callback on a thread-pool thread so it does not block the message loop
                    ThreadPool.QueueUserWorkItem(_ => callback());
                }
                return IntPtr.Zero;
            }

            case WM_USER_REGISTER:
            {
                int id = wParam.ToInt32();
                uint packed = (uint)lParam.ToInt64();
                uint modifiers = packed & 0xFFFF;
                uint vk = (packed >> 16) & 0xFFFF;

                bool ok = RegisterHotKey(hWnd, id, modifiers, vk);

                if (_pendingRegistrations.TryRemove(id, out var tcs))
                    tcs.SetResult(ok);

                return IntPtr.Zero;
            }

            case WM_USER_UNREGISTER:
            {
                int id = wParam.ToInt32();
                UnregisterHotKey(hWnd, id);

                if (_pendingUnregistrations.TryRemove(id, out var tcs))
                    tcs.SetResult(true);

                return IntPtr.Zero;
            }

            case WM_USER_SHUTDOWN:
            {
                // Unregister everything before destroying the window
                foreach (int id in _callbacks.Keys)
                    UnregisterHotKey(hWnd, id);

                DestroyWindow(hWnd);
                return IntPtr.Zero;
            }

            default:
                return DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_USER_SHUTDOWN, IntPtr.Zero, IntPtr.Zero);
            _messageThread.Join(TimeSpan.FromSeconds(5));
        }

        _windowReady.Dispose();
    }
}
