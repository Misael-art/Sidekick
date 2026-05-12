namespace Ajudante.Core.Recorder;

public sealed class MacroRecorderOptions
{
    public bool CaptureMouse { get; set; } = true;
    public bool CaptureKeyboard { get; set; } = true;
    public bool CaptureText { get; set; } = true;
    public bool CaptureSensitiveText { get; set; }
    public string StopHotkey { get; set; } = "Ctrl+Shift+F12";
    public string? TargetProcessName { get; set; }
    public int MaxEvents { get; set; } = 1000;
    public int IdlePauseMs { get; set; } = 1500;
    public string? Goal { get; set; }
}

public sealed class MacroRecordingSession
{
    public required string SessionId { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public string Status { get; set; } = "idle";
    public int EventCount { get; set; }
    public string PrivacyMode { get; set; } = "redactSensitive";
    public string? Goal { get; set; }
}

public sealed class RecorderBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class RecorderWindowContext
{
    public string WindowTitle { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public int ProcessId { get; set; }
    public long WindowHandle { get; set; }
}

public sealed class RecorderElementContext
{
    public string AutomationId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public int ProcessId { get; set; }
    public RecorderBounds? Bounds { get; set; }
    public RecorderBounds? WindowBounds { get; set; }
    public int RelativeX { get; set; }
    public int RelativeY { get; set; }
    public double NormalizedX { get; set; }
    public double NormalizedY { get; set; }
    public int AbsoluteX { get; set; }
    public int AbsoluteY { get; set; }
    public string CursorPixelColor { get; set; } = "";
    public string DetectedText { get; set; } = "";
    public string CurrentText { get; set; } = "";
    public string PlaceholderText { get; set; } = "";
    public string SelectorStrength { get; set; } = "";
    public string SelectorStrategy { get; set; } = "";
    public bool IsBrowserSurface { get; set; }
    public string BrowserUrl { get; set; } = "";
    public string BrowserOrigin { get; set; } = "";
    public string BrowserDocumentTitle { get; set; } = "";
}

public sealed class RecorderMousePayload
{
    public int X { get; set; }
    public int Y { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int EndX { get; set; }
    public int EndY { get; set; }
    public int Delta { get; set; }
    public string Button { get; set; } = "left";
}

public sealed class RecorderKeyboardPayload
{
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public string[] Modifiers { get; set; } = [];
}

public sealed class RecorderTextPayload
{
    public string? Value { get; set; }
    public int Length { get; set; }
    public bool IsRedacted { get; set; }
}

public sealed class RecorderPrivacyInfo
{
    public bool IsRedacted { get; set; }
    public string Mode { get; set; } = "default";
    public string Reason { get; set; } = "";
}

public sealed class RecorderEvent
{
    public required string Id { get; set; }
    public required string Kind { get; set; }
    public required DateTime Timestamp { get; set; }
    public string Label { get; set; } = "";
    public RecorderWindowContext? Window { get; set; }
    public RecorderElementContext? Element { get; set; }
    public RecorderMousePayload? Mouse { get; set; }
    public RecorderKeyboardPayload? Keyboard { get; set; }
    public RecorderTextPayload? Text { get; set; }
    public RecorderPrivacyInfo Privacy { get; set; } = new();
    public double Confidence { get; set; } = 1.0d;
    public List<string> Warnings { get; set; } = [];
}

public sealed class RecorderSuggestedNode
{
    public required string Id { get; set; }
    public required string TypeId { get; set; }
    public RecorderNodePosition Position { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double Confidence { get; set; } = 1.0d;
    public List<string> Warnings { get; set; } = [];
}

public sealed class RecorderNodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class RecorderSuggestedConnection
{
    public required string Id { get; set; }
    public required string SourceNodeId { get; set; }
    public string SourcePort { get; set; } = "out";
    public required string TargetNodeId { get; set; }
    public string TargetPort { get; set; } = "in";
}

public sealed class GuidedAutomationDraft
{
    public required string Id { get; set; }
    public required string SessionId { get; set; }
    public string DisplayName { get; set; } = "Rascunho gravado";
    public bool IsDraft { get; set; } = true;
    public DateTime StartedAt { get; set; }
    public DateTime StoppedAt { get; set; }
    public List<RecorderEvent> Events { get; set; } = [];
    public List<RecorderSuggestedNode> SuggestedNodes { get; set; } = [];
    public List<RecorderSuggestedConnection> SuggestedConnections { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
    public int Score { get; set; } = 100;
    public object? SavedInspectionAsset { get; set; }
}
