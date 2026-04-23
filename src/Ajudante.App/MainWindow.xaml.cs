using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Ajudante.App.Bridge;
using Ajudante.App.TrayIcon;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Registry;
using Microsoft.Web.WebView2.Core;

namespace Ajudante.App;

/// <summary>
/// Main application window. Hosts the WebView2 control, initializes the bridge,
/// engine, and node registry, and manages the system tray icon.
/// </summary>
public partial class MainWindow : Window
{
    private WebBridge? _bridge;
    private BridgeRouter? _router;
    private NodeRegistry? _registry;
    private FlowExecutor? _executor;
    private SystemTrayManager? _trayManager;
    private bool _isCloseConfirmed;
    private bool _isHandlingCloseRequest;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeServicesAsync();
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            MessageBox.Show(
                $"Failed to initialize application:\n\n{ex.Message}",
                "Sidekick - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private async Task InitializeServicesAsync()
    {
        // 1. Initialize node registry and scan built-in nodes
        _registry = new NodeRegistry();
        _registry.ScanAssembly(typeof(Ajudante.Nodes.Actions.MouseClickNode).Assembly);

        // Scan plugins directory for external node DLLs
        _registry.ScanDirectory(App.PluginsDirectory);

        // 2. Initialize flow executor
        _executor = new FlowExecutor(_registry);

        // 3. Initialize WebView2 bridge
        _bridge = new WebBridge(WebView);
        _bridge.LogMessage += OnLogMessage;

        var wwwrootPath = GetWwwrootPath();
        ValidateWebAssets(wwwrootPath);
        await _bridge.InitializeAsync(wwwrootPath);

#if DEBUG
        // In development: try to connect to the Vite dev server for hot-reload.
        // If it's running on localhost:5173, navigate there instead of static files.
        var devServerUrl = "http://localhost:5173";
        if (await IsDevServerRunningAsync(devServerUrl))
        {
            WebView.CoreWebView2.Navigate(devServerUrl);
            System.Diagnostics.Debug.WriteLine($"[Sidekick] Hot-Reload: connected to Vite dev server at {devServerUrl}");
        }
#endif

        // 4. Initialize bridge router
        var flowsDirectory = App.FlowsDirectory;
        _router = new BridgeRouter(_bridge, _registry, _executor, flowsDirectory, Dispatcher);
        _router.LogMessage += OnLogMessage;
        _bridge.SetRouter(_router);

        // Push node definitions to the frontend once the page finishes loading
        WebView.NavigationCompleted += OnWebViewNavigationCompleted;

        // 5. Wire up executor events to forward to the UI
        _executor.NodeStatusChanged += OnNodeStatusChanged;
        _executor.LogMessage += OnExecutorLogMessage;
        _executor.FlowCompleted += OnFlowCompleted;
        _executor.FlowError += OnFlowError;

        // 6. Initialize system tray
        InitializeSystemTray();
    }

    private void InitializeSystemTray()
    {
        _trayManager = new SystemTrayManager(this);
        _trayManager.ShowWindowRequested += () => { };
        _trayManager.QuitRequested += () =>
        {
            Application.Current.Shutdown();
        };
        _trayManager.StartFlowRequested += () =>
        {
            // Could trigger running the last-used flow; for now just show the window
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };
        _trayManager.StopFlowRequested += () =>
        {
            _executor?.Cancel();
        };
        _trayManager.Initialize();
    }

    // ── Executor Event Handlers (forward to React UI via bridge) ─────────

    private async void OnNodeStatusChanged(string nodeId, NodeStatus status)
    {
        if (_bridge == null) return;

        await _bridge.SendEventAsync(
            BridgeMessage.Channels.Engine, "nodeStatusChanged",
            new { nodeId, status = status.ToString().ToLowerInvariant() });

        // Update tray icon state
        if (_trayManager != null)
        {
            _trayManager.IsFlowRunning = _executor?.IsRunning ?? false;
        }
    }

    private async void OnExecutorLogMessage(string nodeId, string message)
    {
        if (_bridge == null) return;

        await _bridge.SendEventAsync(
            BridgeMessage.Channels.Engine, "logMessage",
            new { nodeId, message, timestamp = DateTime.UtcNow });
    }

    private async void OnFlowCompleted(string flowId)
    {
        if (_bridge == null) return;

        await _bridge.SendEventAsync(
            BridgeMessage.Channels.Engine, "flowCompleted",
            new { flowId });

        if (_trayManager != null)
        {
            _trayManager.IsFlowRunning = false;
            _trayManager.ShowBalloon("Flow Completed", "Flow execution finished successfully.");
        }
    }

    private async void OnFlowError(string flowId, string error)
    {
        if (_bridge == null) return;

        await _bridge.SendEventAsync(
            BridgeMessage.Channels.Engine, "flowError",
            new { flowId, error });

        if (_trayManager != null)
        {
            _trayManager.IsFlowRunning = false;
            _trayManager.ShowBalloon("Flow Error", error, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error);
        }
    }

    private void OnLogMessage(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try
        {
            Directory.CreateDirectory(App.LogsDirectory);
            var logFile = Path.Combine(App.LogsDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Keep logging non-fatal.
        }
    }

    private async void OnWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _bridge == null || _registry == null) return;

        // Unsubscribe so we only push once on the initial navigation
        WebView.NavigationCompleted -= OnWebViewNavigationCompleted;

        await _bridge.SendEventAsync(
            BridgeMessage.Channels.Registry, "nodeDefinitions",
            _registry.GetAllDefinitions());
    }

    // ── Title Bar Handlers ───────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Minimize to tray instead of closing
        Hide();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "\uE739"; // Maximize icon
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "\uE923"; // Restore icon
        }
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isCloseConfirmed)
        {
            CleanupResources();
            return;
        }

        if (_isHandlingCloseRequest)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            if (!await ConfirmCloseAsync())
            {
                return;
            }

            _isCloseConfirmed = true;
            _isHandlingCloseRequest = false;
            Close();
            return;
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void CleanupResources()
    {
        _executor?.Cancel();
        _bridge?.Dispose();
        _trayManager?.Dispose();
    }

    private async Task<bool> ConfirmCloseAsync()
    {
        if (!await HasUnsavedChangesAsync())
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "Existem alteracoes nao salvas no fluxo atual.\n\nDeseja sair e descartalas?",
            "Sidekick",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private async Task<bool> HasUnsavedChangesAsync()
    {
        if (_bridge == null || WebView.CoreWebView2 == null)
        {
            return false;
        }

        try
        {
            var result = await WebView.CoreWebView2.ExecuteScriptAsync(
                "window.__sidekickHasUnsavedChanges === true");

            return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            OnLogMessage($"Failed to query unsaved changes state: {ex.Message}");
            return false;
        }
    }

    private static string GetWwwrootPath()
    {
        // In development: look relative to the executable
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;

        // Try several locations
        var candidates = new[]
        {
            Path.Combine(exeDir, "wwwroot"),
            Path.Combine(exeDir, "..", "..", "..", "wwwroot"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "index.html")))
            {
                return fullPath;
            }
        }

        // Default: wwwroot next to executable
        var defaultPath = Path.Combine(exeDir, "wwwroot");
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    private static void ValidateWebAssets(string wwwrootPath)
    {
        var indexPath = Path.Combine(wwwrootPath, "index.html");
        if (!File.Exists(indexPath))
        {
            throw new InvalidOperationException(
                $"Web assets not found at '{wwwrootPath}'. Run 'npm run build' in 'src\\Ajudante.UI' and publish again.");
        }

        var missingAssets = GetMissingWebAssets(indexPath, wwwrootPath);
        if (missingAssets.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Web assets are incomplete. Missing files referenced by index.html: "
            + string.Join(", ", missingAssets)
            + ". This usually happens after a failed publish or an outdated frontend build.");
    }

    private static List<string> GetMissingWebAssets(string indexPath, string wwwrootPath)
    {
        var html = File.ReadAllText(indexPath);
        var matches = Regex.Matches(
            html,
            "(?:src|href)\\s*=\\s*[\"'](?<path>[^\"'#?]+)[\"']",
            RegexOptions.IgnoreCase);

        var missingAssets = new List<string>();
        foreach (Match match in matches)
        {
            var assetPath = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(assetPath)
                || assetPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = assetPath.TrimStart('.', '/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(wwwrootPath, relativePath);
            if (!File.Exists(fullPath))
            {
                missingAssets.Add(assetPath);
            }
        }

        return missingAssets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void LogStartupFailure(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(App.LogsDirectory);
            var logFile = Path.Combine(App.LogsDirectory, $"startup_{DateTime.Now:yyyyMMdd}.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

#if DEBUG
    /// <summary>
    /// Checks whether the Vite dev server is reachable at the given URL.
    /// Used to decide between hot-reload mode and static file mode.
    /// </summary>
    private static async Task<bool> IsDevServerRunningAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
#endif
}
