using Ajudante.App.Configuration;

namespace Ajudante.Nodes.Tests;

public class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sidekick-paths-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Initialize_MigratesLegacyAppDataWithoutDeletingSource()
    {
        Directory.CreateDirectory(_root);

        var legacyRoot = Path.Combine(_root, ProductIdentity.LegacyDataDirectoryName);
        var legacyFlows = Path.Combine(legacyRoot, "flows");
        var legacyLogs = Path.Combine(legacyRoot, "logs");
        var legacyPlugins = Path.Combine(legacyRoot, "plugins");
        Directory.CreateDirectory(legacyFlows);
        Directory.CreateDirectory(legacyLogs);
        Directory.CreateDirectory(legacyPlugins);

        var legacyFlowPath = Path.Combine(legacyFlows, "legacy-flow.json");
        var legacyLogPath = Path.Combine(legacyLogs, "legacy.log");
        var legacyPluginPath = Path.Combine(legacyPlugins, "plugin.dll");

        File.WriteAllText(legacyFlowPath, "{}");
        File.WriteAllText(legacyLogPath, "log");
        File.WriteAllText(legacyPluginPath, "plugin");

        var paths = AppPaths.Initialize(_root);

        Assert.True(paths.MigratedLegacyData);
        Assert.Equal(Path.Combine(_root, ProductIdentity.DataDirectoryName), paths.DataDirectory);
        Assert.True(File.Exists(Path.Combine(paths.FlowsDirectory, "legacy-flow.json")));
        Assert.True(File.Exists(Path.Combine(paths.LogsDirectory, "legacy.log")));
        Assert.True(File.Exists(Path.Combine(paths.PluginsDirectory, "plugin.dll")));
        Assert.True(Directory.Exists(paths.AssetsDirectory));
        Assert.True(Directory.Exists(paths.SnipAssetsDirectory));
        Assert.True(Directory.Exists(paths.InspectionAssetsDirectory));
        Assert.True(Directory.Exists(paths.AssetManifestsDirectory));
        Assert.True(Directory.Exists(legacyRoot));
        Assert.True(File.Exists(legacyFlowPath));
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
}
