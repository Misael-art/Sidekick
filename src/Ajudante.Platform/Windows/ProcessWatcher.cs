using System.Management;

namespace Ajudante.Platform.Windows;

/// <summary>
/// Event data for process lifecycle events.
/// </summary>
public sealed class ProcessEventArgs : EventArgs
{
    /// <summary>The name of the process (e.g. "notepad").</summary>
    public string ProcessName { get; init; } = "";

    /// <summary>The process ID.</summary>
    public int ProcessId { get; init; }
}

/// <summary>
/// Watches for process start and stop events using WMI (ManagementEventWatcher).
/// Requires System.Management and administrative privileges for full functionality.
/// </summary>
public sealed class ProcessWatcher : IDisposable
{
    /// <summary>Raised when a new process is created.</summary>
    public event EventHandler<ProcessEventArgs>? ProcessStarted;

    /// <summary>Raised when a process terminates.</summary>
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private volatile bool _running;

    /// <summary>
    /// Starts watching for process creation and termination events.
    /// </summary>
    /// <param name="pollIntervalSeconds">WMI polling interval in seconds. Lower values
    /// increase responsiveness but also CPU usage. Default is 2.</param>
    public void Start(int pollIntervalSeconds = 2)
    {
        if (_running) return;
        _running = true;

        // WQL query for process creation
        var startQuery = new WqlEventQuery(
            "__InstanceCreationEvent",
            TimeSpan.FromSeconds(pollIntervalSeconds),
            "TargetInstance ISA 'Win32_Process'");

        _startWatcher = new ManagementEventWatcher(startQuery);
        _startWatcher.EventArrived += OnProcessStarted;
        _startWatcher.Start();

        // WQL query for process termination
        var stopQuery = new WqlEventQuery(
            "__InstanceDeletionEvent",
            TimeSpan.FromSeconds(pollIntervalSeconds),
            "TargetInstance ISA 'Win32_Process'");

        _stopWatcher = new ManagementEventWatcher(stopQuery);
        _stopWatcher.EventArrived += OnProcessStopped;
        _stopWatcher.Start();
    }

    /// <summary>
    /// Stops watching for process events.
    /// </summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;

        StopWatcher(ref _startWatcher);
        StopWatcher(ref _stopWatcher);
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        var args = ExtractProcessInfo(e);
        if (args is not null)
        {
            ProcessStarted?.Invoke(this, args);
        }
    }

    private void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        var args = ExtractProcessInfo(e);
        if (args is not null)
        {
            ProcessStopped?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Extracts process name and PID from the WMI event.
    /// </summary>
    private static ProcessEventArgs? ExtractProcessInfo(EventArrivedEventArgs e)
    {
        try
        {
            if (e.NewEvent["TargetInstance"] is not ManagementBaseObject targetInstance)
                return null;

            string processName = targetInstance["Name"]?.ToString() ?? "";
            // Remove .exe extension if present for consistency
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName[..^4];

            int processId = 0;
            object? pidObj = targetInstance["ProcessId"];
            if (pidObj is not null)
                processId = Convert.ToInt32(pidObj);

            return new ProcessEventArgs
            {
                ProcessName = processName,
                ProcessId = processId
            };
        }
        catch
        {
            return null;
        }
    }

    private static void StopWatcher(ref ManagementEventWatcher? watcher)
    {
        if (watcher is null) return;

        try
        {
            watcher.Stop();
            watcher.Dispose();
        }
        catch { /* best effort */ }
        finally
        {
            watcher = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
