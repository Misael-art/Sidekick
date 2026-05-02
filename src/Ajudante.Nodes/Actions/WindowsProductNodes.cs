using System.Diagnostics;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Security;
using Ajudante.Platform.Windows;

namespace Ajudante.Nodes.Actions;

public abstract class WindowsActionNodeBase : IActionNode
{
    protected Dictionary<string, object?> Properties { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";
    public abstract NodeDefinition Definition { get; }
    public abstract Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct);

    public void Configure(Dictionary<string, object?> properties)
    {
        Properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    protected static List<PortDefinition> FlowInput() => new()
    {
        new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
    };

    protected static List<PortDefinition> FlowOutput(params PortDefinition[] extra) =>
        new[] { new PortDefinition { Id = "out", Name = "Out", DataType = PortDataType.Flow } }
            .Concat(extra)
            .ToList();

    protected static List<PropertyDefinition> SafetyProperties(bool dryRunDefault = true) => new()
    {
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = dryRunDefault, Description = "Validate/log without changing Windows" },
        new() { Id = "allowSystemChanges", Name = "Allow System Changes", Type = PropertyType.Boolean, DefaultValue = false, Description = "Required before sensitive Windows changes are executed" }
    };

    protected static NodeResult FromShellResult(ShellOperationResult result)
    {
        if (!result.Success)
            return NodeResult.Fail(result.Message);

        var outputs = new Dictionary<string, object?>(result.Outputs, StringComparer.OrdinalIgnoreCase)
        {
            ["message"] = result.Message
        };
        return NodeResult.Ok("out", outputs);
    }

    protected bool IsDryRun() => NodeValueHelper.GetBool(Properties, "dryRun", true)
        || !NodeValueHelper.GetBool(Properties, "allowSystemChanges", false);

    protected string Text(FlowExecutionContext context, string key, string fallback = "") =>
        NodeValueHelper.ResolveTemplateProperty(context, Properties, key, fallback);

    protected static NodeDefinition Build(string typeId, string displayName, string description, List<PropertyDefinition> properties) => new()
    {
        TypeId = typeId,
        DisplayName = displayName,
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = description,
        InputPorts = FlowInput(),
        OutputPorts = FlowOutput(new PortDefinition { Id = "message", Name = "Message", DataType = PortDataType.String }),
        Properties = properties
    };
}

[NodeInfo(TypeId = "action.requireAdmin", DisplayName = "Require Admin", Category = NodeCategory.Action, Color = "#22C55E", Description = "Checks whether Sidekick is running as administrator and branches safely")]
public sealed class RequireAdminNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => new()
    {
        TypeId = "action.requireAdmin",
        DisplayName = "Require Admin",
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = "Checks current admin/UAC status and routes the flow without bypassing Windows security",
        InputPorts = FlowInput(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "admin", Name = "Admin", DataType = PortDataType.Flow },
            new() { Id = "notAdmin", Name = "Not Admin", DataType = PortDataType.Flow },
            new() { Id = "denied", Name = "Denied", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "message", Name = "Message", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "requireElevation", Name = "Require Elevation", Type = PropertyType.Boolean, DefaultValue = true, Description = "Route denied when admin is mandatory and UAC cannot elevate" }
        }
    };

    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var status = AdminService.GetStatus();
        var port = status.IsAdministrator
            ? "admin"
            : status.CanElevate ? "notAdmin" : "denied";

        return Task.FromResult(NodeResult.Ok(port, new Dictionary<string, object?>
        {
            ["isAdministrator"] = status.IsAdministrator,
            ["canElevate"] = status.CanElevate,
            ["isUacEnabled"] = status.IsUacEnabled,
            ["userName"] = status.UserName,
            ["message"] = status.Message
        }));
    }
}

[NodeInfo(TypeId = "action.restartAsAdmin", DisplayName = "Restart as Admin", Category = NodeCategory.Action, Color = "#22C55E", Description = "Restarts Sidekick through the normal UAC prompt when explicitly confirmed")]
public sealed class RestartAsAdminNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => new()
    {
        TypeId = "action.restartAsAdmin",
        DisplayName = "Restart as Admin",
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = "Uses the Windows runas verb; it never hides or bypasses UAC",
        InputPorts = FlowInput(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "denied", Name = "Denied", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "confirmRestart", Name = "Confirm Restart", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "arguments", Name = "Arguments", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true }
        }
    };

    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var status = AdminService.GetStatus();
        if (status.IsAdministrator)
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = "Sidekick ja esta como administrador." }));

        if (NodeValueHelper.GetBool(Properties, "dryRun", true))
            return Task.FromResult(NodeResult.Ok("denied", new Dictionary<string, object?> { ["message"] = "Dry-run: reinicio com UAC seria solicitado." }));

        if (!NodeValueHelper.GetBool(Properties, "confirmRestart", false))
            return Task.FromResult(NodeResult.Ok("denied", new Dictionary<string, object?> { ["message"] = "confirmRestart precisa ser true para abrir UAC." }));

        try
        {
            AdminService.RestartCurrentProcessAsAdministrator(Text(context, "arguments"));
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = "Prompt UAC solicitado." }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Ok("error", new Dictionary<string, object?> { ["message"] = ex.Message }));
        }
    }
}

[NodeInfo(TypeId = "action.taskbarShow", DisplayName = "Taskbar Show", Category = NodeCategory.Action, Color = "#22C55E", Description = "Shows the Windows taskbar")]
public sealed class TaskbarShowNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarShow", "Taskbar Show", "Shows the Windows taskbar", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetTaskbarVisibility(true, IsDryRun())));
}

[NodeInfo(TypeId = "action.taskbarHide", DisplayName = "Taskbar Hide", Category = NodeCategory.Action, Color = "#22C55E", Description = "Hides the Windows taskbar")]
public sealed class TaskbarHideNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarHide", "Taskbar Hide", "Hides the Windows taskbar", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetTaskbarVisibility(false, IsDryRun())));
}

[NodeInfo(TypeId = "action.taskbarSetAlignment", DisplayName = "Taskbar Set Alignment", Category = NodeCategory.Action, Color = "#22C55E", Description = "Sets Windows 11 taskbar alignment when supported")]
public sealed class TaskbarSetAlignmentNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarSetAlignment", "Taskbar Set Alignment", "Sets Windows 11 taskbar alignment", new List<PropertyDefinition>
    {
        new() { Id = "alignment", Name = "Alignment", Type = PropertyType.Dropdown, DefaultValue = "center", Options = new[] { "left", "center" } }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetTaskbarAlignment(Text(context, "alignment", "center"), IsDryRun())));
}

[NodeInfo(TypeId = "action.taskbarPinApp", DisplayName = "Taskbar Pin App", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents safe limits for pinning apps to the taskbar")]
public sealed class TaskbarPinAppNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarPinApp", "Taskbar Pin App", "Pin app to taskbar when Windows exposes a supported path", new List<PropertyDefinition>
    {
        new() { Id = "appPath", Name = "App Path", Type = PropertyType.FilePath, DefaultValue = "" }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.UnsupportedTaskbarPin("fixar app", IsDryRun())));
}

[NodeInfo(TypeId = "action.taskbarUnpinApp", DisplayName = "Taskbar Unpin App", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents safe limits for unpinning apps from the taskbar")]
public sealed class TaskbarUnpinAppNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarUnpinApp", "Taskbar Unpin App", "Unpin app from taskbar when Windows exposes a supported path", new List<PropertyDefinition>
    {
        new() { Id = "appPath", Name = "App Path", Type = PropertyType.FilePath, DefaultValue = "" }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.UnsupportedTaskbarPin("desafixar app", IsDryRun())));
}

[NodeInfo(TypeId = "action.taskbarOpenPinnedApp", DisplayName = "Taskbar Open Pinned App", Category = NodeCategory.Action, Color = "#22C55E", Description = "Opens an app path or explains when pinned-position automation is unsupported")]
public sealed class TaskbarOpenPinnedAppNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.taskbarOpenPinnedApp", "Taskbar Open Pinned App", "Open a pinned app by appPath when available", new List<PropertyDefinition>
    {
        new() { Id = "appPath", Name = "App Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true }
    });

    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var appPath = Text(context, "appPath");
        if (string.IsNullOrWhiteSpace(appPath))
            return Task.FromResult(NodeResult.Fail("Informe appPath. Abrir por posicao da taskbar nao tem API publica estavel."));
        if (NodeValueHelper.GetBool(Properties, "dryRun", true))
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = "Dry-run: app seria aberto.", ["appPath"] = appPath }));
        Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["appPath"] = appPath }));
    }
}

[NodeInfo(TypeId = "action.windowsThemeSetMode", DisplayName = "Windows Theme Set Mode", Category = NodeCategory.Action, Color = "#22C55E", Description = "Sets light/dark theme via supported user registry keys")]
public sealed class WindowsThemeSetModeNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.windowsThemeSetMode", "Windows Theme Set Mode", "Set light/dark/system theme", new List<PropertyDefinition>
    {
        new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "dark", Options = new[] { "light", "dark", "system" } }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetWindowsThemeMode(Text(context, "mode", "dark"), IsDryRun())));
}

[NodeInfo(TypeId = "action.windowsAccentColor", DisplayName = "Windows Accent Color", Category = NodeCategory.Action, Color = "#22C55E", Description = "Records intended accent color with dry-run safety")]
public sealed class WindowsAccentColorNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.windowsAccentColor", "Windows Accent Color", "Prepare or apply accent color", new List<PropertyDefinition>
    {
        new() { Id = "color", Name = "Color", Type = PropertyType.Color, DefaultValue = "#58A6FF" }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = IsDryRun() ? "Dry-run: cor de destaque validada." : "Accent color registry write is intentionally conservative in this build.", ["color"] = Text(context, "color", "#58A6FF") }));
}

[NodeInfo(TypeId = "action.windowsHighContrast", DisplayName = "Windows High Contrast", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents high contrast automation limits safely")]
public sealed class WindowsHighContrastNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.windowsHighContrast", "Windows High Contrast", "Prepare high contrast state change", new List<PropertyDefinition>
    {
        new() { Id = "enabled", Name = "Enabled", Type = PropertyType.Boolean, DefaultValue = false }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) =>
        Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["message"] = IsDryRun()
                ? "Dry-run: high contrast change validada."
                : "High contrast alternation is version-sensitive; use Windows accessibility settings or UI automation.",
            ["enabled"] = NodeValueHelper.GetBool(Properties, "enabled", false)
        }));
}

[NodeInfo(TypeId = "action.wallpaperSetImage", DisplayName = "Wallpaper Set Image", Category = NodeCategory.Action, Color = "#22C55E", Description = "Sets desktop wallpaper with backup output")]
public sealed class WallpaperSetImageNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.wallpaperSetImage", "Wallpaper Set Image", "Set wallpaper image", new List<PropertyDefinition>
    {
        new() { Id = "imagePath", Name = "Image Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "style", Name = "Style", Type = PropertyType.Dropdown, DefaultValue = "fill", Options = new[] { "fill", "fit", "stretch", "tile", "center", "span" } }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetWallpaperImage(Text(context, "imagePath"), Text(context, "style", "fill"), IsDryRun())));
}

[NodeInfo(TypeId = "action.wallpaperSetColor", DisplayName = "Wallpaper Set Color", Category = NodeCategory.Action, Color = "#22C55E", Description = "Sets desktop background color")]
public sealed class WallpaperSetColorNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.wallpaperSetColor", "Wallpaper Set Color", "Set desktop background color", new List<PropertyDefinition>
    {
        new() { Id = "color", Name = "Color", Type = PropertyType.Color, DefaultValue = "#000000" }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetWallpaperColor(Text(context, "color", "#000000"), IsDryRun())));
}

[NodeInfo(TypeId = "action.wallpaperRestorePrevious", DisplayName = "Wallpaper Restore Previous", Category = NodeCategory.Action, Color = "#22C55E", Description = "Restores a previously captured wallpaper path")]
public sealed class WallpaperRestorePreviousNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.wallpaperRestorePrevious", "Wallpaper Restore Previous", "Restore previous wallpaper path", new List<PropertyDefinition>
    {
        new() { Id = "previousWallpaper", Name = "Previous Wallpaper", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "style", Name = "Style", Type = PropertyType.Dropdown, DefaultValue = "fill", Options = new[] { "fill", "fit", "stretch", "tile", "center", "span" } }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.SetWallpaperImage(Text(context, "previousWallpaper"), Text(context, "style", "fill"), IsDryRun())));
}

[NodeInfo(TypeId = "action.desktopRefresh", DisplayName = "Desktop Refresh", Category = NodeCategory.Action, Color = "#22C55E", Description = "Refreshes Explorer desktop icons")]
public sealed class DesktopRefreshNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.desktopRefresh", "Desktop Refresh", "Refresh desktop/Explorer shell", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.RefreshDesktop(IsDryRun())));
}

[NodeInfo(TypeId = "action.desktopOpenFolder", DisplayName = "Desktop Open Folder", Category = NodeCategory.Action, Color = "#22C55E", Description = "Opens a folder in Explorer")]
public sealed class DesktopOpenFolderNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.desktopOpenFolder", "Desktop Open Folder", "Open folder in Explorer", new List<PropertyDefinition>
    {
        new() { Id = "path", Name = "Path", Type = PropertyType.FolderPath, DefaultValue = "" },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = false }
    });
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.OpenExplorerPath(Text(context, "path"), dryRun: NodeValueHelper.GetBool(Properties, "dryRun", false))));
}

[NodeInfo(TypeId = "action.explorerOpenPath", DisplayName = "Explorer Open Path", Category = NodeCategory.Action, Color = "#22C55E", Description = "Opens a path in Explorer")]
public sealed class ExplorerOpenPathNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.explorerOpenPath", "Explorer Open Path", "Open a path in Explorer", new List<PropertyDefinition>
    {
        new() { Id = "path", Name = "Path", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = false }
    });
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) =>
        Task.FromResult(FromShellResult(WindowsShellService.OpenExplorerPath(Text(context, "path"), dryRun: NodeValueHelper.GetBool(Properties, "dryRun", false))));
}

[NodeInfo(TypeId = "action.explorerSelectFile", DisplayName = "Explorer Select File", Category = NodeCategory.Action, Color = "#22C55E", Description = "Opens Explorer and selects a file")]
public sealed class ExplorerSelectFileNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.explorerSelectFile", "Explorer Select File", "Select a file in Explorer", new List<PropertyDefinition>
    {
        new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = false }
    });
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.OpenExplorerPath(Text(context, "filePath"), selectFile: true, dryRun: NodeValueHelper.GetBool(Properties, "dryRun", false))));
}

[NodeInfo(TypeId = "action.explorerRestart", DisplayName = "Explorer Restart", Category = NodeCategory.Action, Color = "#22C55E", Description = "Restarts explorer.exe with dry-run by default")]
public sealed class ExplorerRestartNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.explorerRestart", "Explorer Restart", "Restart explorer.exe", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.RestartExplorer(IsDryRun())));
}

[NodeInfo(TypeId = "action.explorerSetView", DisplayName = "Explorer Set View", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents Explorer view automation limits")]
public sealed class ExplorerSetViewNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.explorerSetView", "Explorer Set View", "Prepare Explorer view change", new List<PropertyDefinition>
    {
        new() { Id = "view", Name = "View", Type = PropertyType.Dropdown, DefaultValue = "details", Options = new[] { "details", "icons", "list" } }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = "Explorer view changes depend on active shell state; use Mira/keyboard automation for exact windows.", ["dryRun"] = IsDryRun() }));
}

[NodeInfo(TypeId = "action.desktopIconsShowHide", DisplayName = "Desktop Icons Show/Hide", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents safe desktop icon visibility automation")]
public sealed class DesktopIconsShowHideNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.desktopIconsShowHide", "Desktop Icons Show/Hide", "Show or hide desktop icons", new List<PropertyDefinition>
    {
        new() { Id = "visible", Name = "Visible", Type = PropertyType.Boolean, DefaultValue = true }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = IsDryRun() ? "Dry-run: visibilidade dos icones seria alterada." : "Alteracao direta de icones varia por versao; use shortcut/Explorer automation.", ["visible"] = NodeValueHelper.GetBool(Properties, "visible", true) }));
}

[NodeInfo(TypeId = "action.desktopCreateShortcut", DisplayName = "Desktop Create Shortcut", Category = NodeCategory.Action, Color = "#22C55E", Description = "Creates a desktop .lnk shortcut when Windows Script Host is available")]
public sealed class DesktopCreateShortcutNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.desktopCreateShortcut", "Desktop Create Shortcut", "Create shortcut on Desktop", new List<PropertyDefinition>
    {
        new() { Id = "targetPath", Name = "Target Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "shortcutName", Name = "Shortcut Name", Type = PropertyType.String, DefaultValue = "Sidekick Shortcut" }
    }.Concat(SafetyProperties()).ToList());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var target = Text(context, "targetPath");
        var shortcutName = Text(context, "shortcutName", "Sidekick Shortcut");
        if (IsDryRun())
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = "Dry-run: atalho seria criado.", ["targetPath"] = target, ["shortcutName"] = shortcutName }));
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var linkPath = Path.Combine(desktop, $"{shortcutName}.lnk");
        File.WriteAllText(linkPath + ".url", $"[InternetShortcut]{Environment.NewLine}URL=file:///{target.Replace("\\", "/", StringComparison.Ordinal)}");
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["shortcutPath"] = linkPath + ".url" }));
    }
}

[NodeInfo(TypeId = "action.restorePointCreate", DisplayName = "Restore Point Create", Category = NodeCategory.Action, Color = "#22C55E", Description = "Creates or dry-runs a system restore point request")]
public sealed class RestorePointCreateNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.restorePointCreate", "Restore Point Create", "Create Windows restore point", new List<PropertyDefinition>
    {
        new() { Id = "description", Name = "Description", Type = PropertyType.String, DefaultValue = "Sidekick restore point" }
    }.Concat(SafetyProperties()).ToList());

    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var status = AdminService.GetStatus();
        if (!status.IsAdministrator && !IsDryRun())
            return Task.FromResult(NodeResult.Fail("Criar ponto de restauracao requer administrador."));
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?> { ["message"] = IsDryRun() ? "Dry-run: ponto de restauracao seria criado." : "Criacao real deve ser feita via PowerShell/Checkpoint-Computer com consentimento.", ["requiresAdmin"] = true, ["isAdministrator"] = status.IsAdministrator }));
    }
}

[NodeInfo(TypeId = "action.restorePointList", DisplayName = "Restore Point List", Category = NodeCategory.Action, Color = "#22C55E", Description = "Lists restore point support status without restoring the system")]
public sealed class RestorePointListNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.restorePointList", "Restore Point List", "List restore points support status", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var status = AdminService.GetStatus();
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["message"] = "Listagem real de pontos de restauracao depende de WMI/PowerShell e nao restaura o sistema.",
            ["requiresAdmin"] = true,
            ["isAdministrator"] = status.IsAdministrator,
            ["dryRun"] = IsDryRun()
        }));
    }
}

[NodeInfo(TypeId = "action.restorePointOpenSystemRestore", DisplayName = "Open System Restore", Category = NodeCategory.Action, Color = "#22C55E", Description = "Opens Windows System Restore UI; never restores automatically")]
public sealed class RestorePointOpenSystemRestoreNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.restorePointOpenSystemRestore", "Open System Restore", "Open Windows System Restore UI", SafetyProperties());
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(FromShellResult(WindowsShellService.OpenSystemRestore(IsDryRun())));
}

[NodeInfo(TypeId = "action.installApp", DisplayName = "Install App", Category = NodeCategory.Action, Color = "#22C55E", Description = "Installs apps through winget, MSI, EXE or URL with dry-run, retry and verification")]
public sealed class InstallAppNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.installApp", "Install App", "Install app safely from supported source", InstallProperties());
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => FromShellResult(await AppInstallerService.InstallAsync(BuildRequest(context), ct));

    private AppInstallRequest BuildRequest(FlowExecutionContext context) => new(
        SourceType: Text(context, "sourceType", "winget"),
        PackageId: Text(context, "packageId"),
        Url: Text(context, "url"),
        Checksum: Text(context, "checksum"),
        InstallerArgs: Text(context, "installerArgs"),
        Silent: NodeValueHelper.GetBool(Properties, "silent", true),
        RequireAdmin: NodeValueHelper.GetBool(Properties, "requireAdmin", false),
        TimeoutMs: NodeValueHelper.GetInt(Properties, "timeoutMs", 300000),
        RetryCount: NodeValueHelper.GetInt(Properties, "retryCount", 1),
        ExpectedProcessName: Text(context, "expectedProcessName"),
        ExpectedPath: Text(context, "expectedPath"),
        VerifyAfterInstall: NodeValueHelper.GetBool(Properties, "verifyAfterInstall", true),
        DryRun: NodeValueHelper.GetBool(Properties, "dryRun", true));

    public static List<PropertyDefinition> InstallProperties() => new()
    {
        new() { Id = "sourceType", Name = "Source Type", Type = PropertyType.Dropdown, DefaultValue = "winget", Options = new[] { "winget", "msi", "exe", "url", "msix", "store" } },
        new() { Id = "packageId", Name = "Package Id", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "url", Name = "URL / Installer Path", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "checksum", Name = "SHA256 Checksum", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "installerArgs", Name = "Installer Args", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "silent", Name = "Silent", Type = PropertyType.Boolean, DefaultValue = true },
        new() { Id = "requireAdmin", Name = "Require Admin", Type = PropertyType.Boolean, DefaultValue = false },
        new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 300000 },
        new() { Id = "retryCount", Name = "Retry Count", Type = PropertyType.Integer, DefaultValue = 1 },
        new() { Id = "expectedProcessName", Name = "Expected Process", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "expectedPath", Name = "Expected Path", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "verifyAfterInstall", Name = "Verify After Install", Type = PropertyType.Boolean, DefaultValue = true },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true }
    };
}

[NodeInfo(TypeId = "action.installWinget", DisplayName = "Install Winget", Category = NodeCategory.Action, Color = "#22C55E", Description = "Installs a winget package")]
public sealed class InstallWingetNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.installWinget", "Install Winget", "Install winget package", InstallAppNode.InstallProperties());
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var props = new Dictionary<string, object?>(Properties, StringComparer.OrdinalIgnoreCase) { ["sourceType"] = "winget" };
        var node = new InstallAppNode();
        node.Configure(props);
        return await node.ExecuteAsync(context, ct);
    }
}

[NodeInfo(TypeId = "action.installMsi", DisplayName = "Install MSI", Category = NodeCategory.Action, Color = "#22C55E", Description = "Installs an MSI package")]
public sealed class InstallMsiNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.installMsi", "Install MSI", "Install MSI package", InstallAppNode.InstallProperties());
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var props = new Dictionary<string, object?>(Properties, StringComparer.OrdinalIgnoreCase) { ["sourceType"] = "msi" };
        var node = new InstallAppNode();
        node.Configure(props);
        return await node.ExecuteAsync(context, ct);
    }
}

[NodeInfo(TypeId = "action.installExe", DisplayName = "Install EXE", Category = NodeCategory.Action, Color = "#22C55E", Description = "Installs an EXE installer")]
public sealed class InstallExeNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.installExe", "Install EXE", "Install EXE package", InstallAppNode.InstallProperties());
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var props = new Dictionary<string, object?>(Properties, StringComparer.OrdinalIgnoreCase) { ["sourceType"] = "exe" };
        var node = new InstallAppNode();
        node.Configure(props);
        return await node.ExecuteAsync(context, ct);
    }
}

[NodeInfo(TypeId = "action.installMsix", DisplayName = "Install MSIX", Category = NodeCategory.Action, Color = "#22C55E", Description = "Documents MSIX install support")]
public sealed class InstallMsixNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.installMsix", "Install MSIX", "Prepare MSIX install", InstallAppNode.InstallProperties());
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var props = new Dictionary<string, object?>(Properties, StringComparer.OrdinalIgnoreCase) { ["sourceType"] = "msix" };
        var node = new InstallAppNode();
        node.Configure(props);
        return await node.ExecuteAsync(context, ct);
    }
}

[NodeInfo(TypeId = "action.downloadFile", DisplayName = "Download File", Category = NodeCategory.Action, Color = "#22C55E", Description = "Downloads a file with optional checksum verification")]
public sealed class DownloadFileNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.downloadFile", "Download File", "Download URL to file", new List<PropertyDefinition>
    {
        new() { Id = "url", Name = "URL", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "outputPath", Name = "Output Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "checksum", Name = "SHA256 Checksum", Type = PropertyType.String, DefaultValue = "" },
        new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true }
    });
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => FromShellResult(await AppInstallerService.DownloadFileAsync(Text(context, "url"), Text(context, "outputPath"), Text(context, "checksum"), NodeValueHelper.GetBool(Properties, "dryRun", true), ct));
}

[NodeInfo(TypeId = "action.verifyChecksum", DisplayName = "Verify Checksum", Category = NodeCategory.Action, Color = "#22C55E", Description = "Verifies SHA256 checksum")]
public sealed class VerifyChecksumNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => Build("action.verifyChecksum", "Verify Checksum", "Verify SHA256 checksum", new List<PropertyDefinition>
    {
        new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "" },
        new() { Id = "checksum", Name = "SHA256 Checksum", Type = PropertyType.String, DefaultValue = "" }
    });
    public override async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => FromShellResult(await AppInstallerService.VerifyChecksumAsync(Text(context, "filePath"), Text(context, "checksum"), ct));
}

[NodeInfo(TypeId = "action.checkAppInstalled", DisplayName = "Check App Installed", Category = NodeCategory.Action, Color = "#22C55E", Description = "Checks process/path as installation verification")]
public sealed class CheckAppInstalledNode : WindowsActionNodeBase
{
    public override NodeDefinition Definition => new()
    {
        TypeId = "action.checkAppInstalled",
        DisplayName = "Check App Installed",
        Category = NodeCategory.Action,
        Color = "#22C55E",
        Description = "Checks expected process/path",
        InputPorts = FlowInput(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "installed", Name = "Installed", DataType = PortDataType.Flow },
            new() { Id = "notInstalled", Name = "Not Installed", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "expectedProcessName", Name = "Expected Process", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "expectedPath", Name = "Expected Path", Type = PropertyType.String, DefaultValue = "" }
        }
    };
    public override Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var installed = AppInstallerService.IsAppInstalled(Text(context, "expectedProcessName"), Text(context, "expectedPath"));
        return Task.FromResult(NodeResult.Ok(installed ? "installed" : "notInstalled", new Dictionary<string, object?> { ["installed"] = installed }));
    }
}
