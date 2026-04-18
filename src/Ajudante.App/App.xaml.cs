using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ajudante.App;

/// <summary>
/// Application entry point. Handles single-instance enforcement, global exception handling,
/// and application data directory setup.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// Root data directory for Ajudante: %AppData%/Ajudante/
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sidekick");

    /// <summary>
    /// Directory where flow JSON files are stored.
    /// </summary>
    public static string FlowsDirectory { get; } = Path.Combine(DataDirectory, "flows");

    /// <summary>
    /// Directory for application logs.
    /// </summary>
    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// Directory for plugin DLLs containing external nodes.
    /// </summary>
    public static string PluginsDirectory { get; } = Path.Combine(DataDirectory, "plugins");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single instance enforcement
        _singleInstanceMutex = new Mutex(true, "Global\\SidekickRPA_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Sidekick is already running. Check the system tray.",
                "Sidekick",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Ensure application data directories exist
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(FlowsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(PluginsDirectory);

        // Global exception handling
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
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
            "Sidekick - Error",
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
        try
        {
            var logFile = Path.Combine(LogsDirectory, $"crash_{DateTime.Now:yyyyMMdd}.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}\n\n";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Last resort: write to debug output
            System.Diagnostics.Debug.WriteLine($"[{source}] {ex}");
        }
    }
}

