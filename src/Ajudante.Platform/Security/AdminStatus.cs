using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace Ajudante.Platform.Security;

public sealed record AdminStatus(
    bool IsAdministrator,
    bool CanElevate,
    bool IsUacEnabled,
    string UserName,
    string Message);

public static class AdminService
{
    public static AdminStatus GetStatus()
    {
        var isAdmin = IsRunningAsAdministrator();
        var uacEnabled = IsUacEnabled();
        var userName = WindowsIdentity.GetCurrent().Name;
        var canElevate = Environment.UserInteractive && uacEnabled && !isAdmin;
        var message = isAdmin
            ? "Sidekick esta rodando como administrador."
            : canElevate
                ? "Sidekick esta sem privilegios administrativos; o usuario pode reiniciar com UAC."
                : "Sidekick esta sem privilegios administrativos e elevacao nao parece disponivel.";

        return new AdminStatus(isAdmin, canElevate, uacEnabled, userName, message);
    }

    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsUacEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            return key?.GetValue("EnableLUA") is int value && value != 0;
        }
        catch
        {
            return true;
        }
    }

    public static Process? RestartCurrentProcessAsAdministrator(string? arguments = null)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return null;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments ?? "",
            UseShellExecute = true,
            Verb = "runas"
        };

        return Process.Start(startInfo);
    }
}
