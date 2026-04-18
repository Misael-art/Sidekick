using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ajudante.Platform.Windows;

/// <summary>
/// Event data for window events.
/// </summary>
public sealed class WindowEventArgs : EventArgs
{
    /// <summary>The window handle.</summary>
    public IntPtr Hwnd { get; init; }

    /// <summary>The name of the process that owns the window.</summary>
    public string ProcessName { get; init; } = "";

    /// <summary>The window title text.</summary>
    public string WindowTitle { get; init; } = "";

    /// <summary>The process ID that owns the window.</summary>
    public int ProcessId { get; init; }
}

/// <summary>
/// Watches for window lifecycle events (open, close, focus change) using
/// SetWinEventHook. Must be started on a thread with a message pump (STA).
/// Internally creates its own STA thread with a message loop.
/// </summary>
public sealed class WindowWatcher : IDisposable
{
    #region Win32 Interop

    private delegate void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

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

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0;
    private const uint WM_QUIT = 0x0012;

    #endregion

    /// <summary>Raised when a new window is shown.</summary>
    public event EventHandler<WindowEventArgs>? WindowOpened;

    /// <summary>Raised when a window is destroyed.</summary>
    public event EventHandler<WindowEventArgs>? WindowClosed;

    /// <summary>Raised when a different window receives foreground focus.</summary>
    public event EventHandler<WindowEventArgs>? WindowFocused;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private IntPtr _hookShow;
    private IntPtr _hookDestroy;
    private IntPtr _hookForeground;
    private WinEventProc? _winEventDelegate; // prevent GC
    private volatile bool _running;
    private readonly ManualResetEventSlim _ready = new(false);

    /// <summary>
    /// Starts watching for window events. Creates a background STA thread with a message loop.
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;

        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "WindowWatcher"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        _ready.Wait();
    }

    /// <summary>
    /// Stops watching for window events and unhooks all event hooks.
    /// </summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;

        PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hookThread?.Join(TimeSpan.FromSeconds(5));
    }

    private void HookThreadProc()
    {
        _hookThreadId = (uint)Environment.CurrentManagedThreadId;

        // Use AppDomain.GetCurrentThreadId for native thread ID
        _hookThreadId = GetCurrentNativeThreadId();

        _winEventDelegate = OnWinEvent;

        uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

        _hookShow = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW,
            IntPtr.Zero, _winEventDelegate, 0, 0, flags);

        _hookDestroy = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _winEventDelegate, 0, 0, flags);

        _hookForeground = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventDelegate, 0, 0, flags);

        _ready.Set();

        // Message loop
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup hooks
        if (_hookShow != IntPtr.Zero) UnhookWinEvent(_hookShow);
        if (_hookDestroy != IntPtr.Zero) UnhookWinEvent(_hookDestroy);
        if (_hookForeground != IntPtr.Zero) UnhookWinEvent(_hookForeground);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static uint GetCurrentNativeThreadId() => GetCurrentThreadId();

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only care about top-level window objects
        if (idObject != OBJID_WINDOW || idChild != 0 || hwnd == IntPtr.Zero)
            return;

        var args = BuildEventArgs(hwnd);

        switch (eventType)
        {
            case EVENT_OBJECT_SHOW:
                WindowOpened?.Invoke(this, args);
                break;

            case EVENT_OBJECT_DESTROY:
                WindowClosed?.Invoke(this, args);
                break;

            case EVENT_SYSTEM_FOREGROUND:
                WindowFocused?.Invoke(this, args);
                break;
        }
    }

    private static WindowEventArgs BuildEventArgs(IntPtr hwnd)
    {
        string windowTitle = "";
        string processName = "";
        int processId = 0;

        try
        {
            int len = GetWindowTextLength(hwnd);
            if (len > 0)
            {
                var buffer = new char[len + 1];
                GetWindowText(hwnd, buffer, buffer.Length);
                windowTitle = new string(buffer, 0, len);
            }
        }
        catch { /* window may already be gone */ }

        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            processId = (int)pid;

            if (pid != 0)
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
        }
        catch { /* process may have exited */ }

        return new WindowEventArgs
        {
            Hwnd = hwnd,
            ProcessName = processName,
            WindowTitle = windowTitle,
            ProcessId = processId
        };
    }

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
    }
}
