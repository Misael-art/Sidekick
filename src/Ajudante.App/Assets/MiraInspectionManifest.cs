namespace Ajudante.App.Assets;

public sealed class MiraInspectionManifest
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "inspection";
    public int Version { get; set; } = 2;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string DisplayName { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string? Notes { get; set; }
    public MiraInspectionSourceInfo Source { get; set; } = new();
    public MiraInspectionLocator Locator { get; set; } = new();
    public MiraInspectionContent Content { get; set; } = new();
}

public sealed class MiraInspectionSourceInfo
{
    public string? ProcessName { get; set; }
    public string? ProcessPath { get; set; }
    public int? ProcessId { get; set; }
    public string? WindowTitle { get; set; }
}

public sealed class MiraInspectionLocator
{
    public string Strategy { get; set; } = "absolutePositionLastResort";
    public string Strength { get; set; } = "fraca";
    public string? StrengthReason { get; set; }
    public MiraInspectionSelector Selector { get; set; } = new();
    public MiraInspectionBounds RelativeBounds { get; set; } = new();
    public MiraInspectionBounds AbsoluteBounds { get; set; } = new();
    public MiraInspectionFallback Fallback { get; set; } = new();
}

public sealed class MiraInspectionSelector
{
    public string? WindowTitle { get; set; }
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ClassName { get; set; }
    public string? ControlType { get; set; }
}

public sealed class MiraInspectionBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class MiraInspectionContent
{
    public string? Name { get; set; }
    public string? AutomationId { get; set; }
    public string? ClassName { get; set; }
    public string? ControlType { get; set; }
    public string? CursorPixelColor { get; set; }
    public int CursorX { get; set; }
    public int CursorY { get; set; }
    public bool IsFocused { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsVisible { get; set; }
    public int HostScreenWidth { get; set; }
    public int HostScreenHeight { get; set; }
    public string WindowStateAtCapture { get; set; } = "normal";
    public long? WindowHandle { get; set; }
    public string? MonitorDeviceName { get; set; }
    public MiraInspectionBounds MonitorBounds { get; set; } = new();
    public double DpiScale { get; set; } = 1.0;
    public int RelativePointX { get; set; }
    public int RelativePointY { get; set; }
    public double NormalizedWindowX { get; set; }
    public double NormalizedWindowY { get; set; }
    public double NormalizedScreenX { get; set; }
    public double NormalizedScreenY { get; set; }
}

public sealed class MiraInspectionFallback
{
    public bool UseRelativeFallback { get; set; } = true;
    public bool UseScaledFallback { get; set; } = true;
    public bool UseAbsoluteFallback { get; set; } = true;
    public bool RestoreWindowBeforeFallback { get; set; } = true;
    public string ExpectedWindowState { get; set; } = "normal";
    public int RelativeX { get; set; }
    public int RelativeY { get; set; }
    public double NormalizedX { get; set; }
    public double NormalizedY { get; set; }
    public int AbsoluteX { get; set; }
    public int AbsoluteY { get; set; }
}
