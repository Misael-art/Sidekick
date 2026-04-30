using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ajudante.Platform.Hardware;

public sealed record SystemPowerCommandResult(bool Success, string Message);

public static class SystemPowerController
{
    public static async Task<SystemPowerCommandResult> ExecuteAsync(string operation, int delaySeconds, bool forceApps, CancellationToken ct)
    {
        var normalized = operation.Trim().ToLowerInvariant();
        return normalized switch
        {
            "lock" => Lock(),
            "sleep" => Suspend(hibernate: false, forceApps),
            "hibernate" => Suspend(hibernate: true, forceApps),
            "shutdown" => await RunShutdownAsync($"/s /t {Math.Max(0, delaySeconds)}{ForceFlag(forceApps)}", "shutdown scheduled", ct).ConfigureAwait(false),
            "restart" => await RunShutdownAsync($"/r /t {Math.Max(0, delaySeconds)}{ForceFlag(forceApps)}", "restart scheduled", ct).ConfigureAwait(false),
            "logoff" => await RunShutdownAsync($"/l{ForceFlag(forceApps)}", "logoff requested", ct).ConfigureAwait(false),
            "cancelshutdown" => await RunShutdownAsync("/a", "pending shutdown cancelled", ct).ConfigureAwait(false),
            _ => new SystemPowerCommandResult(false, $"Unknown power operation: {operation}")
        };
    }

    private static SystemPowerCommandResult Lock()
    {
        return LockWorkStation()
            ? new SystemPowerCommandResult(true, "workstation locked")
            : new SystemPowerCommandResult(false, "LockWorkStation failed");
    }

    private static SystemPowerCommandResult Suspend(bool hibernate, bool forceApps)
    {
        return SetSuspendState(hibernate, forceApps, disableWakeEvent: false)
            ? new SystemPowerCommandResult(true, hibernate ? "hibernate requested" : "sleep requested")
            : new SystemPowerCommandResult(false, hibernate ? "hibernate failed" : "sleep failed");
    }

    private static async Task<SystemPowerCommandResult> RunShutdownAsync(string arguments, string successMessage, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            return new SystemPowerCommandResult(false, "Unable to start shutdown.exe");

        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return process.ExitCode == 0
            ? new SystemPowerCommandResult(true, successMessage)
            : new SystemPowerCommandResult(false, string.IsNullOrWhiteSpace(stderr) ? $"shutdown.exe exited with {process.ExitCode}" : stderr.Trim());
    }

    private static string ForceFlag(bool forceApps)
    {
        return forceApps ? " /f" : "";
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);
}
