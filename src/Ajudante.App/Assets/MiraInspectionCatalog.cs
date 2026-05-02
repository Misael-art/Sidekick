using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Ajudante.Platform.Screen;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.App.Assets;

public sealed class MiraInspectionCatalog
{
    private readonly string _inspectionAssetsDirectory;
    private readonly string _assetManifestsDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public MiraInspectionCatalog(string dataDirectory, string inspectionAssetsDirectory, string assetManifestsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(inspectionAssetsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetManifestsDirectory);

        _inspectionAssetsDirectory = inspectionAssetsDirectory;
        _assetManifestsDirectory = assetManifestsDirectory;
    }

    public async Task<MiraInspectionManifest> SaveCaptureAsync(
        ElementInfo element,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);

        Directory.CreateDirectory(_inspectionAssetsDirectory);
        Directory.CreateDirectory(_assetManifestsDirectory);

        var now = DateTime.UtcNow;
        var id = $"inspection_{now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        var manifest = CreateManifest(id, element, displayName, now);
        await SaveThumbnailAsync(manifest, element.BoundingRect, cancellationToken);
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.inspection.json");

        await SaveManifestAsync(manifest, cancellationToken);
        return manifest;
    }

    public async Task<IReadOnlyList<MiraInspectionManifest>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_assetManifestsDirectory))
        {
            return [];
        }

        var manifests = new List<MiraInspectionManifest>();
        foreach (var manifestPath in Directory.EnumerateFiles(_assetManifestsDirectory, "*.inspection.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = File.OpenRead(manifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<MiraInspectionManifest>(stream, JsonOptions, cancellationToken);
                if (manifest is not null)
                {
                    EnrichThumbnail(manifest);
                    NormalizeManifest(manifest);
                    manifests.Add(manifest);
                }
            }
            catch
            {
                // Ignore malformed inspection manifests and keep loading the rest.
            }
        }

        return manifests
            .OrderByDescending(asset => asset.UpdatedAt)
            .ThenBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<MiraInspectionManifest?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.inspection.json");
        if (!File.Exists(manifestPath))
            return null;

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<MiraInspectionManifest>(stream, JsonOptions, cancellationToken);
        if (manifest is not null)
        {
            EnrichThumbnail(manifest);
            NormalizeManifest(manifest);
        }

        return manifest;
    }

    public async Task<MiraInspectionManifest?> UpdateAsync(
        string id,
        string? displayName,
        string? notes,
        IEnumerable<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetAsync(id, cancellationToken);
        if (manifest is null)
            return null;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            manifest.DisplayName = displayName.Trim();
        }

        manifest.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (tags is not null)
        {
            manifest.Tags = tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        manifest.UpdatedAt = DateTime.UtcNow;
        NormalizeManifest(manifest);
        await SaveManifestAsync(manifest, cancellationToken);
        return manifest;
    }

    public async Task<MiraInspectionManifest?> DuplicateAsync(
        string id,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var source = await GetAsync(id, cancellationToken);
        if (source is null)
            return null;

        var now = DateTime.UtcNow;
        var copy = JsonSerializer.Deserialize<MiraInspectionManifest>(
            JsonSerializer.Serialize(source, JsonOptions),
            JsonOptions);
        if (copy is null)
            return null;

        copy.Id = $"inspection_{now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        copy.CreatedAt = now;
        copy.UpdatedAt = now;
        copy.DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? $"{source.DisplayName} copia".Trim()
            : displayName.Trim();

        if (!string.IsNullOrWhiteSpace(source.Content.ThumbnailPath))
        {
            var sourcePath = ResolveThumbnailPath(source.Content.ThumbnailPath);
            if (File.Exists(sourcePath))
            {
                var extension = Path.GetExtension(sourcePath);
                var fileName = $"{copy.Id}{(string.IsNullOrWhiteSpace(extension) ? ".png" : extension)}";
                var destinationPath = Path.Combine(_inspectionAssetsDirectory, fileName);
                Directory.CreateDirectory(_inspectionAssetsDirectory);
                File.Copy(sourcePath, destinationPath, overwrite: true);
                copy.Content.ThumbnailPath = fileName;
            }
        }

        EnrichThumbnail(copy);
        NormalizeManifest(copy);
        await SaveManifestAsync(copy, cancellationToken);
        return copy;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(false);

        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.inspection.json");
        if (!File.Exists(manifestPath))
            return Task.FromResult(false);

        MiraInspectionManifest? manifest = null;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            manifest = JsonSerializer.Deserialize<MiraInspectionManifest>(stream, JsonOptions);
        }
        catch
        {
            // Continue deleting the manifest even if it is malformed.
        }

        if (!string.IsNullOrWhiteSpace(manifest?.Content.ThumbnailPath))
        {
            var thumbnailPath = ResolveThumbnailPath(manifest.Content.ThumbnailPath);
            if (File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
            }
        }

        File.Delete(manifestPath);
        return Task.FromResult(true);
    }

    private MiraInspectionManifest CreateManifest(string id, ElementInfo element, string? displayName, DateTime timestampUtc)
    {
        var recommendedStrategy = ResolveStrategy(element);
        var strength = SelectorStrengthEvaluator.Evaluate(element);
        var bounds = ToBounds(element.BoundingRect);
        var relativeBounds = ToBounds(element.RelativeBoundingRect);
        var monitorBounds = ToBounds(element.MonitorBounds);

        return new MiraInspectionManifest
        {
            Id = id,
            SchemaVersion = 1,
            CreatedAt = timestampUtc,
            UpdatedAt = timestampUtc,
            DisplayName = ResolveDisplayName(element, displayName),
            Tags = BuildTags(element),
            Source = new MiraInspectionSourceInfo
            {
                ProcessId = element.ProcessId > 0 ? element.ProcessId : null,
                ProcessName = NullIfWhiteSpace(element.ProcessName) ?? TryResolveProcessName(element.ProcessId),
                ProcessPath = NullIfWhiteSpace(element.ProcessPath),
                WindowTitle = NullIfWhiteSpace(element.WindowTitle)
            },
            Locator = new MiraInspectionLocator
            {
                Strategy = recommendedStrategy,
                Strength = SelectorStrengthEvaluator.ToPublicLabel(strength),
                StrengthReason = SelectorStrengthEvaluator.Explain(element),
                Selector = new MiraInspectionSelector
                {
                    WindowTitle = NullIfWhiteSpace(element.WindowTitle),
                    AutomationId = NullIfWhiteSpace(element.AutomationId),
                    Name = NullIfWhiteSpace(element.Name),
                    ClassName = NullIfWhiteSpace(element.ClassName),
                    ControlType = NullIfWhiteSpace(element.ControlType)
                },
                RelativeBounds = relativeBounds,
                AbsoluteBounds = bounds,
                Fallback = new MiraInspectionFallback
                {
                    UseRelativeFallback = true,
                    UseScaledFallback = true,
                    UseAbsoluteFallback = true,
                    RestoreWindowBeforeFallback = true,
                    ExpectedWindowState = string.IsNullOrWhiteSpace(element.WindowStateAtCapture) ? "normal" : element.WindowStateAtCapture,
                    RelativeX = element.RelativePointX,
                    RelativeY = element.RelativePointY,
                    NormalizedX = element.NormalizedWindowX,
                    NormalizedY = element.NormalizedWindowY,
                    AbsoluteX = element.CursorScreen.X,
                    AbsoluteY = element.CursorScreen.Y
                }
            },
            Content = new MiraInspectionContent
            {
                Name = NullIfWhiteSpace(element.Name),
                AutomationId = NullIfWhiteSpace(element.AutomationId),
                ClassName = NullIfWhiteSpace(element.ClassName),
                ControlType = NullIfWhiteSpace(element.ControlType),
                CursorPixelColor = NullIfWhiteSpace(element.CursorPixelColor),
                DetectedText = NullIfWhiteSpace(element.DetectedText),
                CurrentText = element.CurrentText,
                PlaceholderText = NullIfWhiteSpace(element.PlaceholderText),
                TextSource = NullIfWhiteSpace(element.TextSource),
                CaptureQuality = NullIfWhiteSpace(element.CaptureQuality),
                ValueText = element.ValueText,
                TextPatternText = NullIfWhiteSpace(element.TextPatternText),
                LegacyName = NullIfWhiteSpace(element.LegacyName),
                LegacyValue = NullIfWhiteSpace(element.LegacyValue),
                HelpText = NullIfWhiteSpace(element.HelpText),
                OcrAttempted = element.OcrAttempted,
                OcrAvailable = element.OcrAvailable,
                OcrText = NullIfWhiteSpace(element.OcrText),
                OcrWarning = NullIfWhiteSpace(element.OcrWarning),
                CursorX = element.CursorScreen.X,
                CursorY = element.CursorScreen.Y,
                IsFocused = element.IsFocused,
                IsEnabled = element.IsEnabled,
                IsVisible = !element.IsOffscreen,
                HostScreenWidth = element.HostScreenWidth <= 0 ? (Screen.PrimaryScreen?.Bounds.Width ?? 0) : element.HostScreenWidth,
                HostScreenHeight = element.HostScreenHeight <= 0 ? (Screen.PrimaryScreen?.Bounds.Height ?? 0) : element.HostScreenHeight,
                WindowStateAtCapture = string.IsNullOrWhiteSpace(element.WindowStateAtCapture) ? "normal" : element.WindowStateAtCapture,
                WindowHandle = element.WindowHandle == 0 ? null : element.WindowHandle,
                MonitorDeviceName = NullIfWhiteSpace(element.MonitorDeviceName),
                MonitorBounds = monitorBounds,
                DpiScale = element.DpiScale <= 0 ? 1.0 : element.DpiScale,
                RelativePointX = element.RelativePointX,
                RelativePointY = element.RelativePointY,
                NormalizedWindowX = element.NormalizedWindowX,
                NormalizedWindowY = element.NormalizedWindowY,
                NormalizedScreenX = element.NormalizedScreenX,
                NormalizedScreenY = element.NormalizedScreenY
            }
        };
    }

    private static string ResolveDisplayName(ElementInfo element, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(element.Name))
        {
            return element.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(element.AutomationId))
        {
            return element.AutomationId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(element.ControlType))
        {
            return element.ControlType.Trim();
        }

        return "Captured element";
    }

    private static string ResolveStrategy(ElementInfo element)
    {
        if (!string.IsNullOrWhiteSpace(element.AutomationId))
        {
            return "selectorPreferred";
        }

        if (!string.IsNullOrWhiteSpace(element.Name) || !string.IsNullOrWhiteSpace(element.ControlType))
        {
            return "relativePositionFallback";
        }

        return "absolutePositionLastResort";
    }

    private static List<string> BuildTags(ElementInfo element)
    {
        var tags = new List<string?>
        {
            element.ControlType,
            element.ClassName,
            element.WindowTitle,
            element.ProcessName
        };

        if (element.ControlType.Contains("edit", StringComparison.OrdinalIgnoreCase)
            || element.ControlType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("campo-texto");
        }

        if ((element.DetectedText + " " + element.Name + " " + element.HelpText).Contains("pesquis", StringComparison.OrdinalIgnoreCase)
            || (element.DetectedText + " " + element.Name + " " + element.HelpText).Contains("search", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("busca");
        }

        tags.Add(string.Equals(element.CaptureQuality, "forte", StringComparison.OrdinalIgnoreCase) ? "confiavel" : "revisar");
        if (element.OcrAttempted && !element.OcrAvailable)
        {
            tags.Add("fallback");
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SaveThumbnailAsync(MiraInspectionManifest manifest, Rectangle bounds, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_inspectionAssetsDirectory);
        var fileName = $"{manifest.Id}.png";
        var path = Path.Combine(_inspectionAssetsDirectory, fileName);

        using var bitmap = CaptureThumbnail(bounds);
        bitmap.Save(path, ImageFormat.Png);
        manifest.Content.ThumbnailPath = fileName;
        manifest.Content.ThumbnailBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(path, cancellationToken));
    }

    private static Bitmap CaptureThumbnail(Rectangle bounds)
    {
        try
        {
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                using var capture = ScreenCapture.CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                return ResizeToThumbnail(capture, 320, 200);
            }
        }
        catch
        {
            // Tests and locked desktops may not allow a real screen capture.
        }

        var fallback = new Bitmap(320, 200);
        using var graphics = Graphics.FromImage(fallback);
        graphics.Clear(Color.FromArgb(18, 24, 38));
        using var pen = new Pen(Color.FromArgb(88, 166, 255), 3);
        graphics.DrawRectangle(pen, 8, 8, fallback.Width - 16, fallback.Height - 16);
        using var brush = new SolidBrush(Color.FromArgb(201, 209, 217));
        using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("Mira capture", font, brush, new PointF(22, 80));
        return fallback;
    }

    private static Bitmap ResizeToThumbnail(Bitmap source, int maxWidth, int maxHeight)
    {
        var ratio = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
        {
            ratio = 1;
        }

        ratio = Math.Min(1, ratio);
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
        var thumbnail = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(thumbnail);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, width, height);
        return thumbnail;
    }

    private void EnrichThumbnail(MiraInspectionManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Content.ThumbnailPath))
            return;

        var path = ResolveThumbnailPath(manifest.Content.ThumbnailPath);
        if (!File.Exists(path))
            return;

        manifest.Content.ThumbnailBase64 = Convert.ToBase64String(File.ReadAllBytes(path));
    }

    private void NormalizeManifest(MiraInspectionManifest manifest)
    {
        manifest.SchemaVersion = manifest.SchemaVersion <= 0 ? 1 : manifest.SchemaVersion;
        manifest.Kind = string.IsNullOrWhiteSpace(manifest.Kind) ? "inspection" : manifest.Kind;
        manifest.Tags = manifest.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SaveManifestAsync(MiraInspectionManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_assetManifestsDirectory);
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{manifest.Id}.inspection.json");
        await using var manifestStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
    }

    private string ResolveThumbnailPath(string thumbnailPath)
    {
        return Path.IsPathRooted(thumbnailPath)
            ? thumbnailPath
            : Path.Combine(_inspectionAssetsDirectory, Path.GetFileName(thumbnailPath));
    }

    private static MiraInspectionBounds ToBounds(System.Drawing.Rectangle rectangle)
    {
        return new MiraInspectionBounds
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
    }

    private static string? TryResolveProcessName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
