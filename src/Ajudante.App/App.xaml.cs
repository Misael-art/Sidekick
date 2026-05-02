using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ajudante.App.Configuration;
using Ajudante.Core.Serialization;

namespace Ajudante.App;

/// <summary>
/// Application entry point. Handles single-instance enforcement, global exception handling,
/// and application data directory setup.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// Root data directory for Sidekick.
    /// </summary>
    public static string DataDirectory => AppPaths.Current.DataDirectory;

    /// <summary>
    /// Directory where flow JSON files are stored.
    /// </summary>
    public static string FlowsDirectory => AppPaths.Current.FlowsDirectory;

    /// <summary>
    /// Directory for application logs.
    /// </summary>
    public static string LogsDirectory => AppPaths.Current.LogsDirectory;

    /// <summary>
    /// Directory for plugin DLLs containing external nodes.
    /// </summary>
    public static string PluginsDirectory => AppPaths.Current.PluginsDirectory;

    /// <summary>
    /// Root directory for persisted product assets.
    /// </summary>
    public static string AssetsDirectory => AppPaths.Current.AssetsDirectory;

    /// <summary>
    /// Directory for persisted Snip image files.
    /// </summary>
    public static string SnipAssetsDirectory => AppPaths.Current.SnipAssetsDirectory;

    /// <summary>
    /// Directory for persisted Mira inspection assets.
    /// </summary>
    public static string InspectionAssetsDirectory => AppPaths.Current.InspectionAssetsDirectory;

    /// <summary>
    /// Directory for persisted asset manifests.
    /// </summary>
    public static string AssetManifestsDirectory => AppPaths.Current.AssetManifestsDirectory;

    public static string? StartupRunFlowPath { get; private set; }
    public static bool ExitAfterStartupRun { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var paths = AppPaths.Initialize();
        ParseStartupArguments(e.Args);

        // Single instance enforcement
        _singleInstanceMutex = new Mutex(true, ProductIdentity.MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                $"{ProductIdentity.ProductName} is already running. Check the system tray.",
                ProductIdentity.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        if (paths.MigratedLegacyData)
        {
            LogInformation("Startup", $"Migrated legacy data from '{paths.LegacyDataDirectory}' to '{paths.DataDirectory}'.");
        }

        SeedBundledFlows();

        // Global exception handling
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private static void ParseStartupArguments(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--run-flow", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                StartupRunFlowPath = args[++index];
                continue;
            }

            if (string.Equals(arg, "--exit-after-run", StringComparison.OrdinalIgnoreCase))
            {
                ExitAfterStartupRun = true;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will attempt to continue.",
            ProductIdentity.ErrorTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("DomainUnhandledException", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogException(string source, Exception ex)
    {
        LogInformation(source, ex.ToString(), $"crash_{DateTime.Now:yyyyMMdd}.log");
    }

    private static void LogInformation(string source, string message, string? fileName = null)
    {
        try
        {
            var logFile = Path.Combine(LogsDirectory, fileName ?? $"app_{DateTime.Now:yyyyMMdd}.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Last resort: write to debug output
            System.Diagnostics.Debug.WriteLine($"[{source}] {message}");
        }
    }

    private static void SeedBundledFlows()
    {
        try
        {
            var bundledFlowsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows");
            if (!Directory.Exists(bundledFlowsDirectory))
            {
                return;
            }

            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in Directory.EnumerateFiles(FlowsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var flow = FlowSerializer.Deserialize(File.ReadAllText(filePath));
                    if (!string.IsNullOrWhiteSpace(flow?.Id))
                    {
                        existingIds.Add(flow.Id);
                    }
                }
                catch
                {
                    // Ignore malformed user files while seeding bundled samples.
                }
            }

            foreach (var sourcePath in Directory.EnumerateFiles(bundledFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var flow = FlowSerializer.Deserialize(File.ReadAllText(sourcePath));
                    if (string.IsNullOrWhiteSpace(flow?.Id) || existingIds.Contains(flow.Id))
                    {
                        continue;
                    }

                    var destinationPath = Path.Combine(FlowsDirectory, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                    existingIds.Add(flow.Id);
                }
                catch
                {
                    // Keep startup resilient even if a bundled sample is malformed.
                }
            }
        }
        catch
        {
            // Seeding sample flows is best-effort only.
        }
    }
}

