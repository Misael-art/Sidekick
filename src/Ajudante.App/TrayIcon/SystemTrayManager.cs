using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace Ajudante.App.TrayIcon;

/// <summary>
/// Manages the system tray icon, context menu, and tray interactions.
/// Uses Hardcodet.NotifyIcon.Wpf for WPF-native tray icon support.
/// All UI access is marshalled to the dispatcher thread.
/// </summary>
public class SystemTrayManager : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly Window _mainWindow;
    private readonly Dispatcher _dispatcher;
    private bool _isDisposed;

    public event Action? ShowWindowRequested;
    public event Action? QuitRequested;
    public event Action? StartFlowRequested;
    public event Action? StopFlowRequested;

    public bool IsFlowRunning
    {
        get => _isFlowRunning;
        set
        {
            _isFlowRunning = value;
            RunOnUI(() => UpdateTrayState());
        }
    }
    private volatile bool _isFlowRunning;

    public SystemTrayManager(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _dispatcher = mainWindow.Dispatcher;
    }

    /// <summary>
    /// Creates and shows the system tray icon with context menu.
    /// </summary>
    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Sidekick - Visual Automation",
            Visibility = Visibility.Visible
        };

        // Use a generated icon since we may not have an .ico file yet
        _trayIcon.Icon = CreateDefaultIcon();

        // Build the context menu
        _trayIcon.ContextMenu = BuildContextMenu();

        // Double-click shows the main window
        _trayIcon.TrayMouseDoubleClick += OnTrayDoubleClick;

        UpdateTrayState();
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        RunOnUI(() => _trayIcon?.ShowBalloonTip(title, message, icon));
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Show Window
        var showItem = new System.Windows.Controls.MenuItem { Header = "Show Window" };
        showItem.Click += (_, _) => OnShowWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Start/Stop Flow
        var flowItem = new System.Windows.Controls.MenuItem
        {
            Header = _isFlowRunning ? "Stop Flow" : "Start Flow"
        };
        flowItem.Click += (_, _) =>
        {
            if (_isFlowRunning)
                StopFlowRequested?.Invoke();
            else
                StartFlowRequested?.Invoke();
        };
        menu.Items.Add(flowItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Quit
        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => QuitRequested?.Invoke();
        menu.Items.Add(quitItem);

        return menu;
    }

    private void UpdateTrayState()
    {
        if (_trayIcon == null) return;

        _trayIcon.ToolTipText = _isFlowRunning
            ? "Sidekick - Flow Running"
            : "Sidekick - Visual Automation";

        // Rebuild context menu to reflect current state
        _trayIcon.ContextMenu = BuildContextMenu();
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        OnShowWindow();
    }

    private void OnShowWindow()
    {
        RunOnUI(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
        ShowWindowRequested?.Invoke();
    }

    /// <summary>
    /// Creates a simple default icon programmatically.
    /// This is a placeholder until a proper .ico resource is added.
    /// </summary>
    private static Icon CreateDefaultIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        // Dark background with a cyan "A" shape
        g.Clear(Color.FromArgb(30, 30, 46));

        using var pen = new Pen(Color.FromArgb(0, 200, 200), 2.5f);
        // Draw a stylized "A"
        g.DrawLine(pen, 8, 26, 16, 6);
        g.DrawLine(pen, 16, 6, 24, 26);
        g.DrawLine(pen, 11, 18, 21, 18);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        RunOnUI(() =>
        {
            if (_trayIcon != null)
            {
                _trayIcon.TrayMouseDoubleClick -= OnTrayDoubleClick;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        });
    }

    private void RunOnUI(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.InvokeAsync(action);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);
}
