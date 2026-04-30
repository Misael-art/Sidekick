using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
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
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.inspection.json");

        await using var manifestStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
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
        return await JsonSerializer.DeserializeAsync<MiraInspectionManifest>(stream, JsonOptions, cancellationToken);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(false);

        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.inspection.json");
        if (!File.Exists(manifestPath))
            return Task.FromResult(false);

        File.Delete(manifestPath);
        return Task.FromResult(true);
    }

    private MiraInspectionManifest CreateManifest(string id, ElementInfo element, string? displayName, DateTime timestampUtc)
    {
        var recommendedStrategy = ResolveStrategy(element);
        var strength = SelectorStrengthEvaluator.Evaluate(element);
        var bounds = ToBounds(element.BoundingRect);
        var relativeBounds = ToBounds(element.RelativeBoundingRect);

        return new MiraInspectionManifest
        {
            Id = id,
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
                AbsoluteBounds = bounds
            },
            Content = new MiraInspectionContent
            {
                Name = NullIfWhiteSpace(element.Name),
                AutomationId = NullIfWhiteSpace(element.AutomationId),
                ClassName = NullIfWhiteSpace(element.ClassName),
                ControlType = NullIfWhiteSpace(element.ControlType),
                CursorPixelColor = NullIfWhiteSpace(element.CursorPixelColor),
                CursorX = element.CursorScreen.X,
                CursorY = element.CursorScreen.Y,
                IsFocused = element.IsFocused,
                IsEnabled = element.IsEnabled,
                IsVisible = !element.IsOffscreen,
                HostScreenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 0,
                HostScreenHeight = Screen.PrimaryScreen?.Bounds.Height ?? 0
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
        var tags = new[]
        {
            element.ControlType,
            element.ClassName,
            element.WindowTitle
        };

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
