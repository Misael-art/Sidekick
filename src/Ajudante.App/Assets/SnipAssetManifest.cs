namespace Ajudante.App.Assets;

public sealed class SnipAssetManifest
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "snip";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string DisplayName { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string? Notes { get; set; }
    public SnipAssetSourceInfo Source { get; set; } = new();
    public SnipAssetBounds CaptureBounds { get; set; } = new();
    public SnipAssetContent Content { get; set; } = new();
}

public sealed class SnipAssetSourceInfo
{
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
    public string? WindowTitle { get; set; }
    public string? WindowClassName { get; set; }
}

public sealed class SnipAssetBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class SnipAssetContent
{
    public string ImagePath { get; set; } = "";
    public string? OcrText { get; set; }
    public double? OcrConfidence { get; set; }
}
