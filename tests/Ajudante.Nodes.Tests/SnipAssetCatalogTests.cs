using System.Drawing;
using Ajudante.App.Assets;

namespace Ajudante.Nodes.Tests;

public class SnipAssetCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sidekick-snip-assets-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveCaptureAsync_PersistsManifestAndImage()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var snipAssetsDirectory = Path.Combine(dataDirectory, "assets", "snips");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new SnipAssetCatalog(dataDirectory, snipAssetsDirectory, assetManifestsDirectory);

        var pngBytes = CreateFakePngBytes();
        var bounds = new Rectangle(10, 20, 30, 40);

        var asset = await catalog.SaveCaptureAsync(pngBytes, bounds, "Header Button");

        Assert.False(string.IsNullOrWhiteSpace(asset.Id));
        Assert.Equal("snip", asset.Kind);
        Assert.Equal("Header Button", asset.DisplayName);
        Assert.Equal(10, asset.CaptureBounds.X);
        Assert.Equal(20, asset.CaptureBounds.Y);
        Assert.Equal(30, asset.CaptureBounds.Width);
        Assert.Equal(40, asset.CaptureBounds.Height);
        Assert.StartsWith("assets/snips/", asset.Content.ImagePath, StringComparison.Ordinal);

        var absoluteImagePath = Path.Combine(dataDirectory, asset.Content.ImagePath.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(assetManifestsDirectory, $"{asset.Id}.snip.json");

        Assert.True(File.Exists(absoluteImagePath));
        Assert.True(File.Exists(manifestPath));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(absoluteImagePath));
    }

    [Fact]
    public async Task ListAsync_ReturnsPersistedAssetsNewestFirst()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var snipAssetsDirectory = Path.Combine(dataDirectory, "assets", "snips");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new SnipAssetCatalog(dataDirectory, snipAssetsDirectory, assetManifestsDirectory);

        var older = await catalog.SaveCaptureAsync(CreateFakePngBytes(), new Rectangle(0, 0, 10, 10), "Older Snip");
        await Task.Delay(20);
        var newer = await catalog.SaveCaptureAsync(CreateFakePngBytes(), new Rectangle(5, 5, 20, 20), "Newer Snip");

        var assets = await catalog.ListAsync();

        Assert.Equal(2, assets.Count);
        Assert.Equal(newer.Id, assets[0].Id);
        Assert.Equal(older.Id, assets[1].Id);
    }

    [Fact]
    public async Task SaveCaptureAsync_RejectsEmptyPayload()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var snipAssetsDirectory = Path.Combine(dataDirectory, "assets", "snips");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new SnipAssetCatalog(dataDirectory, snipAssetsDirectory, assetManifestsDirectory);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            catalog.SaveCaptureAsync([], new Rectangle(0, 0, 1, 1)));

        Assert.Equal("pngBytes", exception.ParamName);
    }

    [Fact]
    public async Task GetImageBase64Async_ReturnsPersistedImageBytes()
    {
        Directory.CreateDirectory(_root);
        var dataDirectory = Path.Combine(_root, "data");
        var snipAssetsDirectory = Path.Combine(dataDirectory, "assets", "snips");
        var assetManifestsDirectory = Path.Combine(dataDirectory, "assets", "manifests");
        var catalog = new SnipAssetCatalog(dataDirectory, snipAssetsDirectory, assetManifestsDirectory);

        var pngBytes = CreateFakePngBytes();
        var asset = await catalog.SaveCaptureAsync(pngBytes, new Rectangle(10, 20, 30, 40), "Template");

        var manifest = await catalog.GetAsync(asset.Id);
        var imageBase64 = await catalog.GetImageBase64Async(asset.Id);

        Assert.NotNull(manifest);
        Assert.Equal(asset.Id, manifest!.Id);
        Assert.Equal(Convert.ToBase64String(pngBytes), imageBase64);
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

    private static byte[] CreateFakePngBytes()
    {
        return
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 1, 0, 0, 0, 1,
            8, 6, 0, 0, 0, 31, 21, 196,
            137, 0, 0, 0, 12, 73, 68, 65,
            84, 120, 156, 99, 248, 15, 4, 0,
            9, 251, 3, 253, 160, 90, 121, 162,
            0, 0, 0, 0, 73, 69, 78, 68,
            174, 66, 96, 130
        ];
    }
}
