using System.Diagnostics;
using System.Text;

namespace Ajudante.Platform.Hardware;

public sealed record HardwareDeviceCommandResult(int ExitCode, string Stdout, string Stderr, string CommandSummary);

public static class HardwareDeviceController
{
    public static Task<HardwareDeviceCommandResult> ListDevicesAsync(string nameFilter, CancellationToken ct)
    {
        var filter = EscapePowerShellSingleQuoted(nameFilter);
        var script = string.IsNullOrWhiteSpace(nameFilter)
            ? "Get-PnpDevice -PresentOnly | Where-Object { $_.Class -in @('Camera','Image','AudioEndpoint','Net') } | Select-Object Class,FriendlyName,Status,InstanceId | ConvertTo-Json -Compress"
            : $"Get-PnpDevice -PresentOnly | Where-Object {{ $_.Class -in @('Camera','Image','AudioEndpoint','Net') -and ($_.FriendlyName -match '{filter}' -or $_.InstanceId -match '{filter}') }} | Select-Object Class,FriendlyName,Status,InstanceId | ConvertTo-Json -Compress";

        return RunPowerShellAsync(script, "list hardware devices", ct);
    }

    public static Task<HardwareDeviceCommandResult> SetCameraEnabledAsync(bool enabled, string nameFilter, CancellationToken ct)
    {
        return SetPnpDeviceEnabledAsync(["Camera", "Image"], DefaultFilter(nameFilter, "Camera|Webcam|Integrated Camera"), enabled, "camera", ct);
    }

    public static Task<HardwareDeviceCommandResult> SetMicrophoneDeviceEnabledAsync(bool enabled, string nameFilter, CancellationToken ct)
    {
        return SetPnpDeviceEnabledAsync(["AudioEndpoint"], DefaultFilter(nameFilter, "Microphone|Microfone|Mic"), enabled, "microphone device", ct);
    }

    public static Task<HardwareDeviceCommandResult> SetWifiEnabledAsync(bool enabled, string nameFilter, CancellationToken ct)
    {
        return SetPnpDeviceEnabledAsync(["Net"], DefaultFilter(nameFilter, "Wi-Fi|WiFi|Wireless|802\\.11|WLAN"), enabled, "wifi adapter", ct);
    }

    private static Task<HardwareDeviceCommandResult> SetPnpDeviceEnabledAsync(
        IReadOnlyCollection<string> classes,
        string nameFilter,
        bool enabled,
        string summary,
        CancellationToken ct)
    {
        var classList = string.Join(",", classes.Select(item => $"'{EscapePowerShellSingleQuoted(item)}'"));
        var filter = EscapePowerShellSingleQuoted(nameFilter);
        var verb = enabled ? "Enable-PnpDevice" : "Disable-PnpDevice";
        var script = string.Join(Environment.NewLine,
            $"$devices = Get-PnpDevice -PresentOnly | Where-Object {{ @({classList}) -contains $_.Class -and ($_.FriendlyName -match '{filter}' -or $_.InstanceId -match '{filter}') }}",
            $"if (-not $devices) {{ Write-Error 'No matching {summary} found.'; exit 2 }}",
            $"$devices | ForEach-Object {{ {verb} -InstanceId $_.InstanceId -Confirm:$false -ErrorAction Stop }}",
            "$devices | Select-Object Class,FriendlyName,Status,InstanceId | ConvertTo-Json -Compress");

        return RunPowerShellAsync(script, $"{(enabled ? "enable" : "disable")} {summary}", ct);
    }

    private static async Task<HardwareDeviceCommandResult> RunPowerShellAsync(string script, string commandSummary, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {QuoteArgument(script)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return new HardwareDeviceCommandResult(process.ExitCode, stdout.ToString(), stderr.ToString(), commandSummary);
    }

    private static string DefaultFilter(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
