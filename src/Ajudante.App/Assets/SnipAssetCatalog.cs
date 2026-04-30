using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajudante.App.Assets;

public sealed class SnipAssetCatalog
{
    private readonly string _dataDirectory;
    private readonly string _snipAssetsDirectory;
    private readonly string _assetManifestsDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public SnipAssetCatalog(string dataDirectory, string snipAssetsDirectory, string assetManifestsDirectory)
    {
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _snipAssetsDirectory = snipAssetsDirectory ?? throw new ArgumentNullException(nameof(snipAssetsDirectory));
        _assetManifestsDirectory = assetManifestsDirectory ?? throw new ArgumentNullException(nameof(assetManifestsDirectory));

        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_snipAssetsDirectory);
        Directory.CreateDirectory(_assetManifestsDirectory);
    }

    public async Task<SnipAssetManifest> SaveCaptureAsync(
        byte[] pngBytes,
        Rectangle bounds,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            throw new ArgumentException("Snip capture payload is empty.", nameof(pngBytes));
        }

        var id = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow;
        var imageFileName = $"{id}.png";
        var imagePath = Path.Combine(_snipAssetsDirectory, imageFileName);
        var manifestPath = Path.Combine(_assetManifestsDirectory, $"{id}.snip.json");

        await File.WriteAllBytesAsync(imagePath, pngBytes, cancellationToken);

        var manifest = new SnipAssetManifest
        {
            Id = id,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            DisplayName = BuildDisplayName(displayName, timestamp),
            Source = TryResolveSourceInfo(bounds),
            CaptureBounds = new SnipAssetBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            },
            Content = new SnipAssetContent
            {
                ImagePath = Path.GetRelativePath(_dataDirectory, imagePath).Replace('\\', '/')
            }
        };

        await using var manifestStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
        return manifest;
    }

    public async Task<IReadOnlyList<SnipAssetManifest>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_assetManifestsDirectory))
        {
            return [];
        }

        var assets = new List<SnipAssetManifest>();
        foreach (var manifestPath in Directory.EnumerateFiles(_assetManifestsDirectory, "*.snip.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<SnipAssetManifest>(stream, JsonOptions, cancellationToken);
            if (manifest != null && string.Equals(manifest.Kind, "snip", StringComparison.OrdinalIgnoreCase))
            {
                assets.Add(manifest);
            }
        }

        return assets
            .OrderByDescending(asset => asset.UpdatedAt)
            .ThenBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<SnipAssetManifest?> GetAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var manifestPath = GetManifestPath(assetId);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<SnipAssetManifest>(stream, JsonOptions, cancellationToken);
        return manifest != null && string.Equals(manifest.Kind, "snip", StringComparison.OrdinalIgnoreCase)
            ? manifest
            : null;
    }

    public async Task<string?> GetImageBase64Async(string assetId, CancellationToken cancellationToken = default)
    {
        var manifest = await GetAsync(assetId, cancellationToken);
        if (manifest == null)
        {
            return null;
        }

        var imagePath = ResolveContentPath(manifest.Content.ImagePath);
        if (!File.Exists(imagePath))
        {
            return null;
        }

        var pngBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return pngBytes.Length == 0 ? null : Convert.ToBase64String(pngBytes);
    }

    private static string BuildDisplayName(string? displayName, DateTime timestamp)
    {
        var trimmed = displayName?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Snip {timestamp:yyyy-MM-dd HH-mm-ss}"
            : trimmed;
    }

    private string GetManifestPath(string assetId)
    {
        var normalizedAssetId = NormalizeAssetId(assetId);
        return Path.Combine(_assetManifestsDirectory, $"{normalizedAssetId}.snip.json");
    }

    private string ResolveContentPath(string imagePath)
    {
        if (Path.IsPathRooted(imagePath))
        {
            return imagePath;
        }

        return Path.Combine(_dataDirectory, imagePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string NormalizeAssetId(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var normalized = Path.GetFileName(assetId.Trim());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Asset id is invalid.", nameof(assetId));
        }

        return normalized;
    }

    private static SnipAssetSourceInfo TryResolveSourceInfo(Rectangle bounds)
    {
        try
        {
            var point = new NativePoint
            {
                X = bounds.Left + Math.Max(0, bounds.Width / 2),
                Y = bounds.Top + Math.Max(0, bounds.Height / 2)
            };

            var windowHandle = WindowFromPoint(point);
            if (windowHandle == IntPtr.Zero)
            {
                return new SnipAssetSourceInfo();
            }

            GetWindowThreadProcessId(windowHandle, out var processId);
            var windowTitle = ReadWindowText(windowHandle);
            var windowClassName = ReadClassName(windowHandle);

            string? processName = null;
            if (processId != 0)
            {
                try
                {
                    processName = Process.GetProcessById((int)processId).ProcessName;
                }
                catch
                {
                    processName = null;
                }
            }

            return new SnipAssetSourceInfo
            {
                ProcessId = processId == 0 ? null : (int)processId,
                ProcessName = processName,
                WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle,
                WindowClassName = string.IsNullOrWhiteSpace(windowClassName) ? null : windowClassName
            };
        }
        catch
        {
            return new SnipAssetSourceInfo();
        }
    }

    private static string ReadWindowText(IntPtr windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(windowHandle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(IntPtr windowHandle)
    {
        var builder = new StringBuilder(256);
        _ = GetClassName(windowHandle, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
