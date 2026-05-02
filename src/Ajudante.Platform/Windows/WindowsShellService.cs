using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Ajudante.Platform.Windows;

public sealed record ShellOperationResult(bool Success, string Message, Dictionary<string, object?> Outputs);

public static class WindowsShellService
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, string? pvParam, int fWinIni);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static ShellOperationResult SetTaskbarVisibility(bool visible, bool dryRun)
    {
        if (dryRun)
        {
            return Ok("Dry-run: a barra de tarefas seria " + (visible ? "exibida." : "ocultada."), ("visible", visible));
        }

        var handle = FindWindow("Shell_TrayWnd", null);
        if (handle == IntPtr.Zero)
        {
            return Fail("Barra de tarefas nao encontrada pelo Shell_TrayWnd.");
        }

        var success = ShowWindow(handle, visible ? SW_SHOW : SW_HIDE);
        return success
            ? Ok("Barra de tarefas atualizada.", ("visible", visible))
            : Fail("Windows recusou atualizar a visibilidade da barra de tarefas.");
    }

    public static ShellOperationResult SetTaskbarAlignment(string alignment, bool dryRun)
    {
        var normalized = string.Equals(alignment, "left", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        if (dryRun)
        {
            return Ok("Dry-run: alinhamento da taskbar seria gravado no registro HKCU.", ("alignment", alignment));
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        key?.SetValue("TaskbarAl", normalized, RegistryValueKind.DWord);
        return Ok("Alinhamento da taskbar salvo. Pode ser necessario reiniciar o Explorer.", ("alignment", alignment), ("requiresExplorerRestart", true));
    }

    public static ShellOperationResult UnsupportedTaskbarPin(string operation, bool dryRun)
    {
        var message = $"Windows nao oferece API publica estavel para {operation} na taskbar; use dry-run/atalho ou acao manual assistida.";
        return dryRun ? Ok("Dry-run: " + message, ("supported", false)) : Fail(message);
    }

    public static ShellOperationResult OpenExplorerPath(string path, bool selectFile = false, bool dryRun = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail("Informe um caminho para o Explorer.");
        }

        if (dryRun)
        {
            return Ok("Dry-run: Explorer seria aberto.", ("path", path), ("selectFile", selectFile));
        }

        var argument = selectFile ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
        return Ok("Explorer aberto.", ("path", path), ("selectFile", selectFile));
    }

    public static ShellOperationResult RestartExplorer(bool dryRun)
    {
        if (dryRun)
        {
            return Ok("Dry-run: explorer.exe seria reiniciado.", ("processName", "explorer"));
        }

        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            process.Kill(entireProcessTree: true);
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        return Ok("Explorer reiniciado.", ("processName", "explorer"));
    }

    public static ShellOperationResult RefreshDesktop(bool dryRun)
    {
        if (dryRun)
        {
            return Ok("Dry-run: refresh do desktop seria disparado.", ("event", "SHCNE_ASSOCCHANGED"));
        }

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        return Ok("Desktop/Explorer atualizados.", ("event", "SHCNE_ASSOCCHANGED"));
    }

    public static ShellOperationResult SetWallpaperImage(string imagePath, string style, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return Fail("Informe imagePath para trocar o papel de parede.");
        }

        if (!File.Exists(imagePath))
        {
            return Fail($"Imagem nao encontrada: {imagePath}");
        }

        var previous = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop")?.GetValue("WallPaper")?.ToString() ?? "";
        if (dryRun)
        {
            return Ok("Dry-run: papel de parede seria alterado.", ("imagePath", imagePath), ("previousWallpaper", previous), ("style", style));
        }

        ApplyWallpaperStyle(style);
        var success = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        return success
            ? Ok("Papel de parede alterado.", ("imagePath", imagePath), ("previousWallpaper", previous), ("style", style))
            : Fail("Windows recusou alterar o papel de parede.");
    }

    public static ShellOperationResult SetWallpaperColor(string color, bool dryRun)
    {
        if (dryRun)
        {
            return Ok("Dry-run: cor de fundo do desktop seria alterada.", ("color", color));
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Colors");
        key?.SetValue("Background", ConvertHexToRgbString(color));
        return Ok("Cor de fundo salva. Pode ser necessario atualizar o desktop.", ("color", color));
    }

    public static ShellOperationResult SetWindowsThemeMode(string mode, bool dryRun)
    {
        var normalized = mode.Equals("dark", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        if (dryRun)
        {
            return Ok("Dry-run: tema do Windows seria alterado em HKCU.", ("mode", mode));
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        key?.SetValue("AppsUseLightTheme", normalized, RegistryValueKind.DWord);
        key?.SetValue("SystemUsesLightTheme", normalized, RegistryValueKind.DWord);
        return Ok("Tema salvo. Alguns apps podem exigir reinicio/relogin.", ("mode", mode), ("mayRequireRestart", true));
    }

    public static ShellOperationResult OpenSystemRestore(bool dryRun)
    {
        if (dryRun)
        {
            return Ok("Dry-run: a UI de Restauracao do Sistema seria aberta.", ("command", "rstrui.exe"));
        }

        Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true });
        return Ok("Restauracao do Sistema aberta.", ("command", "rstrui.exe"));
    }

    private static void ApplyWallpaperStyle(string style)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
        var normalized = style.Trim().ToLowerInvariant();
        key?.SetValue("TileWallpaper", normalized == "tile" ? "1" : "0");
        key?.SetValue("WallpaperStyle", normalized switch
        {
            "center" => "0",
            "tile" => "0",
            "stretch" => "2",
            "fit" => "6",
            "fill" => "10",
            "span" => "22",
            _ => "10"
        });
    }

    private static string ConvertHexToRgbString(string color)
    {
        var hex = color.Trim().TrimStart('#');
        if (hex.Length != 6)
            return "0 0 0";

        return $"{Convert.ToInt32(hex[..2], 16)} {Convert.ToInt32(hex.Substring(2, 2), 16)} {Convert.ToInt32(hex.Substring(4, 2), 16)}";
    }

    private static ShellOperationResult Ok(string message, params (string Key, object? Value)[] outputs) =>
        new(true, message, outputs.ToDictionary(item => item.Key, item => item.Value));

    private static ShellOperationResult Fail(string message) => new(false, message, new Dictionary<string, object?>());
}
