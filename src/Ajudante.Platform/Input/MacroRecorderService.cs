using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ajudante.Core.Recorder;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Platform.Input;

public sealed class MacroRecorderEventArgs : EventArgs
{
    public MacroRecorderEventArgs(RecorderEvent recorderEvent)
    {
        Event = recorderEvent;
    }

    public RecorderEvent Event { get; }
}

public sealed class MacroRecorderHotkeyEventArgs : EventArgs
{
    public MacroRecorderHotkeyEventArgs(string hotkey)
    {
        Hotkey = hotkey;
    }

    public string Hotkey { get; }
}

public sealed class MacroRecorderStopResult
{
    public required MacroRecordingSession Session { get; init; }
    public required IReadOnlyList<RecorderEvent> Events { get; init; }
}

public sealed class MacroRecorderService : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private readonly object _gate = new();
    private readonly List<RecorderEvent> _events = [];
    private HookProc? _mouseProc;
    private HookProc? _keyboardProc;
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private MacroRecorderOptions _options = new();
    private MacroRecordingSession? _session;
    private bool _disposed;

    public event EventHandler<MacroRecorderEventArgs>? EventCaptured;
    public event EventHandler<MacroRecorderHotkeyEventArgs>? StopHotkeyPressed;

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _session?.Status == "recording";
            }
        }
    }

    public MacroRecordingSession GetStatus()
    {
        lock (_gate)
        {
            if (_session is null)
            {
                return new MacroRecordingSession
                {
                    SessionId = "",
                    StartedAt = DateTime.MinValue,
                    Status = "idle",
                    EventCount = 0,
                    PrivacyMode = "redactSensitive"
                };
            }

            _session.EventCount = _events.Count;
            return CloneSession(_session);
        }
    }

    public MacroRecordingSession Start(MacroRecorderOptions? options = null)
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_session is not null && _session.Status == "recording")
            {
                throw new InvalidOperationException("Macro recorder is already active.");
            }

            _options = NormalizeOptions(options);
            _events.Clear();
            _session = new MacroRecordingSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTime.UtcNow,
                Status = "recording",
                EventCount = 0,
                PrivacyMode = _options.CaptureSensitiveText ? "captureSensitive" : "redactSensitive",
                Goal = _options.Goal
            };

            try
            {
                InstallHooks();
            }
            catch
            {
                UninstallHooks();
                _events.Clear();
                _session = null;
                throw;
            }

            return CloneSession(_session);
        }
    }

    public MacroRecorderStopResult Stop()
    {
        lock (_gate)
        {
            if (_session is null || _session.Status != "recording")
            {
                throw new InvalidOperationException("Macro recorder is not active.");
            }

            UninstallHooks();
            _session.Status = "stopped";
            _session.StoppedAt = DateTime.UtcNow;
            _session.EventCount = _events.Count;
            return new MacroRecorderStopResult
            {
                Session = CloneSession(_session),
                Events = _events.Select(CloneEvent).ToList()
            };
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            UninstallHooks();
            _events.Clear();
            if (_session is not null)
            {
                _session.Status = "cancelled";
                _session.StoppedAt = DateTime.UtcNow;
                _session.EventCount = 0;
            }

            _session = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cancel();
        _disposed = true;
    }

    private void InstallHooks()
    {
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
        var moduleHandle = GetModuleHandle(null);

        if (_options.CaptureMouse)
        {
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
            if (_mouseHook == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to install mouse hook ({Marshal.GetLastWin32Error()}).");
            }
        }

        if (_options.CaptureKeyboard)
        {
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            if (_keyboardHook == IntPtr.Zero)
            {
                UninstallHooks();
                throw new InvalidOperationException($"Failed to install keyboard hook ({Marshal.GetLastWin32Error()}).");
            }
        }
    }

    private void UninstallHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            try
            {
                var hook = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var kind = ResolveMouseKind(wParam.ToInt32());
                if (kind is not null)
                {
                    var mouse = new RecorderMousePayload
                    {
                        X = hook.pt.X,
                        Y = hook.pt.Y,
                        Button = ResolveMouseButton(wParam.ToInt32()),
                        Delta = wParam.ToInt32() == WM_MOUSEWHEEL ? unchecked((short)((hook.mouseData >> 16) & 0xffff)) : 0
                    };
                    var element = kind == "mouseUp" || kind == "mouseClick" || kind == "mouseDown"
                        ? CaptureElement(hook.pt.X, hook.pt.Y)
                        : null;
                    AppendEvent(new RecorderEvent
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = kind,
                        Timestamp = DateTime.UtcNow,
                        Label = DescribeMouseKind(kind),
                        Window = CaptureWindowContext(),
                        Element = element,
                        Mouse = mouse,
                        Confidence = element is null ? 0.65d : 0.9d
                    });
                }
            }
            catch
            {
                // Hooks must never destabilize user input; failed enrichment is ignored.
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording && (wParam.ToInt32() == WM_KEYDOWN || wParam.ToInt32() == WM_SYSKEYDOWN))
        {
            try
            {
                var hook = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var modifiers = CaptureModifiers();
                var key = ResolveKeyName(hook.vkCode);
                var text = _options.CaptureText ? ResolveText(hook.vkCode, hook.scanCode) : "";
                var window = CaptureWindowContext();
                var element = CaptureElementUnderCursor();
                var isStopHotkey = MatchesStopHotkey(key, modifiers, _options.StopHotkey);
                var likelySensitive = IsLikelySensitiveElement(element);

                AppendEvent(new RecorderEvent
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = isStopHotkey ? "hotkey" : likelySensitive && !_options.CaptureSensitiveText ? "redactedInput" : "keyPress",
                    Timestamp = DateTime.UtcNow,
                    Label = isStopHotkey ? "Hotkey de parada" : "Tecla pressionada",
                    Window = window,
                    Element = element,
                    Keyboard = new RecorderKeyboardPayload
                    {
                        Key = key,
                        Text = likelySensitive && !_options.CaptureSensitiveText ? "" : text,
                        Modifiers = modifiers
                    },
                    Text = likelySensitive && !_options.CaptureSensitiveText && !string.IsNullOrEmpty(text)
                        ? new RecorderTextPayload { Value = null, Length = text.Length, IsRedacted = true }
                        : null,
                    Privacy = likelySensitive && !_options.CaptureSensitiveText
                        ? new RecorderPrivacyInfo
                        {
                            IsRedacted = true,
                            Mode = "redactSensitive",
                            Reason = "Campo sob o cursor parece sensivel."
                        }
                        : new RecorderPrivacyInfo(),
                    Confidence = 0.85d
                });

                if (isStopHotkey)
                {
                    StopHotkeyPressed?.Invoke(this, new MacroRecorderHotkeyEventArgs(_options.StopHotkey));
                }
            }
            catch
            {
                // Hooks must remain best-effort and non-blocking.
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void AppendEvent(RecorderEvent recorderEvent)
    {
        MacroRecorderEventArgs? args = null;
        lock (_gate)
        {
            if (_session is null || _session.Status != "recording")
            {
                return;
            }

            if (_events.Count >= Math.Max(1, _options.MaxEvents))
            {
                return;
            }

            _events.Add(recorderEvent);
            _session.EventCount = _events.Count;
            args = new MacroRecorderEventArgs(CloneEvent(recorderEvent));
        }

        EventCaptured?.Invoke(this, args);
    }

    private RecorderWindowContext CaptureWindowContext()
    {
        var hwnd = GetForegroundWindow();
        var title = "";
        var processName = "";
        var processPath = "";
        var processId = 0;

        if (hwnd != IntPtr.Zero)
        {
            var buffer = new StringBuilder(512);
            if (GetWindowText(hwnd, buffer, buffer.Capacity) > 0)
            {
                title = buffer.ToString();
            }

            _ = GetWindowThreadProcessId(hwnd, out var pid);
            processId = unchecked((int)pid);
            if (processId > 0)
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    processName = process.ProcessName ?? "";
                    try
                    {
                        processPath = process.MainModule?.FileName ?? "";
                    }
                    catch
                    {
                        processPath = "";
                    }
                }
                catch
                {
                    processName = "";
                }
            }
        }

        return new RecorderWindowContext
        {
            WindowTitle = title,
            ProcessName = processName,
            ProcessPath = processPath,
            ProcessId = processId,
            WindowHandle = hwnd.ToInt64()
        };
    }

    private static RecorderElementContext? CaptureElement(int x, int y)
    {
        try
        {
            var element = ElementInspector.GetElementAtPoint(x, y);
            return element is null ? null : MapElement(element, x, y);
        }
        catch
        {
            return null;
        }
    }

    private static RecorderElementContext? CaptureElementUnderCursor()
    {
        try
        {
            var element = ElementInspector.GetElementUnderCursor();
            return element is null
                ? null
                : MapElement(element, element.CursorScreen.X, element.CursorScreen.Y);
        }
        catch
        {
            return null;
        }
    }

    private static RecorderElementContext MapElement(ElementInfo element, int cursorX, int cursorY)
    {
        return new RecorderElementContext
        {
            AutomationId = element.AutomationId,
            Name = element.Name,
            ClassName = element.ClassName,
            ControlType = element.ControlType,
            WindowTitle = element.WindowTitle,
            ProcessName = element.ProcessName,
            ProcessPath = element.ProcessPath,
            ProcessId = element.ProcessId,
            Bounds = new RecorderBounds
            {
                X = element.BoundingRect.X,
                Y = element.BoundingRect.Y,
                Width = element.BoundingRect.Width,
                Height = element.BoundingRect.Height
            },
            WindowBounds = new RecorderBounds
            {
                X = element.WindowBounds.X,
                Y = element.WindowBounds.Y,
                Width = element.WindowBounds.Width,
                Height = element.WindowBounds.Height
            },
            RelativeX = element.WindowBounds.Width > 0 ? cursorX - element.WindowBounds.X : element.RelativePointX,
            RelativeY = element.WindowBounds.Height > 0 ? cursorY - element.WindowBounds.Y : element.RelativePointY,
            NormalizedX = element.NormalizedWindowX,
            NormalizedY = element.NormalizedWindowY,
            AbsoluteX = cursorX,
            AbsoluteY = cursorY,
            SelectorStrength = element.CaptureQuality,
            SelectorStrategy = string.IsNullOrWhiteSpace(element.AutomationId) ? "relativePositionFallback" : "selectorPreferred"
        };
    }

    private static MacroRecorderOptions NormalizeOptions(MacroRecorderOptions? options)
    {
        var normalized = options ?? new MacroRecorderOptions();
        normalized.MaxEvents = Math.Clamp(normalized.MaxEvents <= 0 ? 1000 : normalized.MaxEvents, 50, 5000);
        normalized.IdlePauseMs = Math.Clamp(normalized.IdlePauseMs <= 0 ? 1500 : normalized.IdlePauseMs, 250, 30000);
        normalized.StopHotkey = string.IsNullOrWhiteSpace(normalized.StopHotkey)
            ? "Ctrl+Shift+F12"
            : normalized.StopHotkey.Trim();
        return normalized;
    }

    private static string? ResolveMouseKind(int message)
    {
        return message switch
        {
            WM_MOUSEMOVE => "mouseMove",
            WM_MOUSEWHEEL => "mouseWheel",
            WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN => "mouseDown",
            WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP => "mouseUp",
            _ => null
        };
    }

    private static string ResolveMouseButton(int message)
    {
        return message switch
        {
            WM_RBUTTONDOWN or WM_RBUTTONUP => "right",
            WM_MBUTTONDOWN or WM_MBUTTONUP => "middle",
            _ => "left"
        };
    }

    private static string DescribeMouseKind(string kind)
    {
        return kind switch
        {
            "mouseDown" => "Botao do mouse pressionado",
            "mouseUp" => "Botao do mouse solto",
            "mouseWheel" => "Roda do mouse",
            _ => kind
        };
    }

    private static string[] CaptureModifiers()
    {
        var modifiers = new List<string>();
        if (IsKeyDown(VK_CONTROL)) modifiers.Add("Ctrl");
        if (IsKeyDown(VK_SHIFT)) modifiers.Add("Shift");
        if (IsKeyDown(VK_MENU)) modifiers.Add("Alt");
        if (IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN)) modifiers.Add("Win");
        return modifiers.ToArray();
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetKeyState(virtualKey) & 0x8000) != 0;
    }

    private static string ResolveKeyName(uint vkCode)
    {
        if (vkCode >= 0x30 && vkCode <= 0x39)
        {
            return ((char)vkCode).ToString();
        }

        if (vkCode >= 0x41 && vkCode <= 0x5A)
        {
            return ((char)vkCode).ToString();
        }

        return vkCode switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Return",
            0x1B => "Escape",
            0x20 => "Space",
            0x2E => "Delete",
            _ => $"VK_{vkCode:X2}"
        };
    }

    private static string ResolveText(uint vkCode, uint scanCode)
    {
        try
        {
            var keyboardState = new byte[256];
            if (!GetKeyboardState(keyboardState))
            {
                return "";
            }

            var buffer = new StringBuilder(8);
            var result = ToUnicode(vkCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);
            return result > 0 ? buffer.ToString(0, result) : "";
        }
        catch
        {
            return "";
        }
    }

    private static bool MatchesStopHotkey(string key, IReadOnlyCollection<string> modifiers, string stopHotkey)
    {
        if (string.IsNullOrWhiteSpace(stopHotkey))
        {
            return false;
        }

        var parts = stopHotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var expectedKey = parts[^1];
        var expectedModifiers = parts.Take(parts.Length - 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Equals(expectedKey, key, StringComparison.OrdinalIgnoreCase)
            && expectedModifiers.SetEquals(modifiers);
    }

    private static bool IsLikelySensitiveElement(RecorderElementContext? element)
    {
        if (element is null)
        {
            return false;
        }

        var haystack = $"{element.Name} {element.AutomationId} {element.ClassName} {element.ControlType}".ToLowerInvariant();
        return haystack.Contains("password", StringComparison.Ordinal)
            || haystack.Contains("senha", StringComparison.Ordinal)
            || haystack.Contains("token", StringComparison.Ordinal)
            || haystack.Contains("secret", StringComparison.Ordinal)
            || haystack.Contains("api key", StringComparison.Ordinal)
            || haystack.Contains("apikey", StringComparison.Ordinal);
    }

    private static MacroRecordingSession CloneSession(MacroRecordingSession session)
    {
        return new MacroRecordingSession
        {
            SessionId = session.SessionId,
            StartedAt = session.StartedAt,
            StoppedAt = session.StoppedAt,
            Status = session.Status,
            EventCount = session.EventCount,
            PrivacyMode = session.PrivacyMode,
            Goal = session.Goal
        };
    }

    private static RecorderEvent CloneEvent(RecorderEvent source)
    {
        return new RecorderEvent
        {
            Id = source.Id,
            Kind = source.Kind,
            Timestamp = source.Timestamp,
            Label = source.Label,
            Window = source.Window is null ? null : new RecorderWindowContext
            {
                WindowTitle = source.Window.WindowTitle,
                ProcessName = source.Window.ProcessName,
                ProcessPath = source.Window.ProcessPath,
                ProcessId = source.Window.ProcessId,
                WindowHandle = source.Window.WindowHandle
            },
            Element = source.Element is null ? null : new RecorderElementContext
            {
                AutomationId = source.Element.AutomationId,
                Name = source.Element.Name,
                ClassName = source.Element.ClassName,
                ControlType = source.Element.ControlType,
                WindowTitle = source.Element.WindowTitle,
                ProcessName = source.Element.ProcessName,
                ProcessPath = source.Element.ProcessPath,
                ProcessId = source.Element.ProcessId,
                Bounds = CloneBounds(source.Element.Bounds),
                WindowBounds = CloneBounds(source.Element.WindowBounds),
                RelativeX = source.Element.RelativeX,
                RelativeY = source.Element.RelativeY,
                NormalizedX = source.Element.NormalizedX,
                NormalizedY = source.Element.NormalizedY,
                AbsoluteX = source.Element.AbsoluteX,
                AbsoluteY = source.Element.AbsoluteY,
                SelectorStrength = source.Element.SelectorStrength,
                SelectorStrategy = source.Element.SelectorStrategy
            },
            Mouse = source.Mouse is null ? null : new RecorderMousePayload
            {
                X = source.Mouse.X,
                Y = source.Mouse.Y,
                StartX = source.Mouse.StartX,
                StartY = source.Mouse.StartY,
                EndX = source.Mouse.EndX,
                EndY = source.Mouse.EndY,
                Delta = source.Mouse.Delta,
                Button = source.Mouse.Button
            },
            Keyboard = source.Keyboard is null ? null : new RecorderKeyboardPayload
            {
                Key = source.Keyboard.Key,
                Text = source.Keyboard.Text,
                Modifiers = [.. source.Keyboard.Modifiers]
            },
            Text = source.Text is null ? null : new RecorderTextPayload
            {
                Value = source.Text.Value,
                Length = source.Text.Length,
                IsRedacted = source.Text.IsRedacted
            },
            Privacy = new RecorderPrivacyInfo
            {
                IsRedacted = source.Privacy.IsRedacted,
                Mode = source.Privacy.Mode,
                Reason = source.Privacy.Reason
            },
            Confidence = source.Confidence,
            Warnings = [.. source.Warnings]
        };
    }

    private static RecorderBounds? CloneBounds(RecorderBounds? source)
    {
        return source is null
            ? null
            : new RecorderBounds
            {
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);
}
