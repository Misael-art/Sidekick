using System.IO;

namespace Ajudante.App.Configuration;

public sealed record AppPathSet(
    string DataDirectory,
    string LegacyDataDirectory,
    string FlowsDirectory,
    string LogsDirectory,
    string PluginsDirectory,
    string AssetsDirectory,
    string SnipAssetsDirectory,
    string InspectionAssetsDirectory,
    string AssetManifestsDirectory,
    string WebView2DataDirectory,
    bool MigratedLegacyData);

public static class AppPaths
{
    public static AppPathSet Current { get; private set; } = Resolve(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static AppPathSet Resolve(string appDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataRoot);

        var dataDirectory = Path.Combine(appDataRoot, ProductIdentity.DataDirectoryName);
        var legacyDataDirectory = Path.Combine(appDataRoot, ProductIdentity.LegacyDataDirectoryName);

        return new AppPathSet(
            dataDirectory,
            legacyDataDirectory,
            Path.Combine(dataDirectory, "flows"),
            Path.Combine(dataDirectory, "logs"),
            Path.Combine(dataDirectory, "plugins"),
            Path.Combine(dataDirectory, "assets"),
            Path.Combine(dataDirectory, "assets", "snips"),
            Path.Combine(dataDirectory, "assets", "inspections"),
            Path.Combine(dataDirectory, "assets", "manifests"),
            Path.Combine(dataDirectory, "WebView2Data"),
            MigratedLegacyData: false);
    }

    public static AppPathSet Initialize(string? appDataRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(appDataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : appDataRoot;

        var resolved = Resolve(root);
        var officialExists = Directory.Exists(resolved.DataDirectory);
        var legacyExists = Directory.Exists(resolved.LegacyDataDirectory);
        var migrated = false;

        if (!officialExists && legacyExists)
        {
            CopyDirectory(resolved.LegacyDataDirectory, resolved.DataDirectory);
            migrated = true;
        }

        Directory.CreateDirectory(resolved.DataDirectory);
        Directory.CreateDirectory(resolved.FlowsDirectory);
        Directory.CreateDirectory(resolved.LogsDirectory);
        Directory.CreateDirectory(resolved.PluginsDirectory);
        Directory.CreateDirectory(resolved.AssetsDirectory);
        Directory.CreateDirectory(resolved.SnipAssetsDirectory);
        Directory.CreateDirectory(resolved.InspectionAssetsDirectory);
        Directory.CreateDirectory(resolved.AssetManifestsDirectory);
        Directory.CreateDirectory(resolved.WebView2DataDirectory);

        Current = resolved with { MigratedLegacyData = migrated };
        return Current;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);

            var destinationFolder = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
        }
    }
}
