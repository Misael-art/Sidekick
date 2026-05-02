using System.Drawing;
using Ajudante.App.Assets;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Tests;

public class MiraInspectionCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sidekick-mira-assets-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveCaptureAsync_PersistsInspectionManifest()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var inspectionAssetsDirectory = Path.Combine(dataDirectory, "assets", "inspections");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new MiraInspectionCatalog(dataDirectory, inspectionAssetsDirectory, assetManifestsDirectory);

        var captured = CreateElementInfo(
            automationId: "submit-button",
            name: "Submit",
            className: "Button",
            controlType: "button",
            bounds: new Rectangle(10, 20, 80, 24),
            windowTitle: "Checkout");

        var asset = await catalog.SaveCaptureAsync(captured, "Primary CTA");

        Assert.False(string.IsNullOrWhiteSpace(asset.Id));
        Assert.Equal("inspection", asset.Kind);
        Assert.Equal(1, asset.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(asset.Content.ThumbnailPath));
        Assert.True(File.Exists(Path.Combine(inspectionAssetsDirectory, Path.GetFileName(asset.Content.ThumbnailPath))));
        Assert.Equal("Primary CTA", asset.DisplayName);
        Assert.Equal("selectorPreferred", asset.Locator.Strategy);
        Assert.Equal("submit-button", asset.Locator.Selector.AutomationId);
        Assert.Equal("Checkout", asset.Source.WindowTitle);
        Assert.Equal(10, asset.Locator.AbsoluteBounds.X);
        Assert.Equal(20, asset.Locator.AbsoluteBounds.Y);
        Assert.Equal(80, asset.Locator.AbsoluteBounds.Width);
        Assert.Equal(24, asset.Locator.AbsoluteBounds.Height);

        var manifestPath = Path.Combine(assetManifestsDirectory, $"{asset.Id}.inspection.json");
        Assert.True(File.Exists(manifestPath));
    }

    [Fact]
    public async Task ListAsync_ReturnsPersistedAssetsNewestFirst()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var inspectionAssetsDirectory = Path.Combine(dataDirectory, "assets", "inspections");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new MiraInspectionCatalog(dataDirectory, inspectionAssetsDirectory, assetManifestsDirectory);

        var older = await catalog.SaveCaptureAsync(CreateElementInfo(name: "Older", controlType: "text"));
        await Task.Delay(20);
        var newer = await catalog.SaveCaptureAsync(CreateElementInfo(name: "Newer", controlType: "button"));

        var assets = await catalog.ListAsync();

        Assert.Equal(2, assets.Count);
        Assert.Equal(newer.Id, assets[0].Id);
        Assert.Equal(older.Id, assets[1].Id);
    }

    [Fact]
    public async Task SaveCaptureAsync_FallsBackToRelativeStrategyWithoutAutomationId()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var inspectionAssetsDirectory = Path.Combine(dataDirectory, "assets", "inspections");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new MiraInspectionCatalog(dataDirectory, inspectionAssetsDirectory, assetManifestsDirectory);

        var asset = await catalog.SaveCaptureAsync(CreateElementInfo(
            automationId: "",
            name: "Search",
            controlType: "edit"));

        Assert.Equal("relativePositionFallback", asset.Locator.Strategy);
        Assert.Equal("Search", asset.Content.Name);
        Assert.Equal("edit", asset.Content.ControlType);
    }

    [Fact]
    public async Task SaveCaptureAsync_PersistsDetectedTextAndDefaultTags()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var inspectionAssetsDirectory = Path.Combine(dataDirectory, "assets", "inspections");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new MiraInspectionCatalog(dataDirectory, inspectionAssetsDirectory, assetManifestsDirectory);

        var asset = await catalog.SaveCaptureAsync(CreateElementInfo(
            automationId: "search-box",
            name: "Pesquisar",
            controlType: "edit",
            valueText: "",
            helpText: "Pesquisar",
            textSource: "Name",
            captureQuality: "forte"));

        Assert.Equal("Pesquisar", asset.Content.DetectedText);
        Assert.Equal("", asset.Content.CurrentText);
        Assert.Equal("Pesquisar", asset.Content.PlaceholderText);
        Assert.Equal("Name", asset.Content.TextSource);
        Assert.Equal("forte", asset.Content.CaptureQuality);
        Assert.Contains("campo-texto", asset.Tags);
        Assert.Contains("busca", asset.Tags);
        Assert.Contains("confiavel", asset.Tags);
    }

    [Fact]
    public async Task UpdateAndDuplicateAsync_EditLibraryMetadataWithoutLosingLocator()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var inspectionAssetsDirectory = Path.Combine(dataDirectory, "assets", "inspections");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new MiraInspectionCatalog(dataDirectory, inspectionAssetsDirectory, assetManifestsDirectory);

        var asset = await catalog.SaveCaptureAsync(CreateElementInfo(automationId: "ok", name: "OK", controlType: "button"));
        var updated = await catalog.UpdateAsync(asset.Id, "Botao OK", "Confirmacao principal", ["botao", "checkout"]);
        var duplicate = await catalog.DuplicateAsync(asset.Id, "Botao OK copia");

        Assert.NotNull(updated);
        Assert.Equal("Botao OK", updated.DisplayName);
        Assert.Equal("Confirmacao principal", updated.Notes);
        Assert.Contains("checkout", updated.Tags);
        Assert.NotNull(duplicate);
        Assert.NotEqual(asset.Id, duplicate.Id);
        Assert.Equal("Botao OK copia", duplicate.DisplayName);
        Assert.Equal(asset.Locator.Selector.AutomationId, duplicate.Locator.Selector.AutomationId);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Temporary directory cleanup is best effort for tests.
        }
    }

    private static ElementInfo CreateElementInfo(
        string automationId = "",
        string name = "",
        string className = "",
        string controlType = "",
        Rectangle? bounds = null,
        int processId = 0,
        string windowTitle = "",
        string valueText = "",
        string helpText = "",
        string textSource = "",
        string captureQuality = "")
    {
        return new ElementInfo
        {
            AutomationId = automationId,
            Name = name,
            ClassName = className,
            ControlType = controlType,
            BoundingRect = bounds ?? new Rectangle(0, 0, 10, 10),
            ProcessId = processId,
            WindowTitle = windowTitle,
            ValueText = valueText,
            HelpText = helpText,
            DetectedText = string.IsNullOrWhiteSpace(name) ? valueText : name,
            CurrentText = valueText,
            PlaceholderText = helpText,
            TextSource = textSource,
            CaptureQuality = captureQuality
        };
    }
}
