using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Hardware;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.systemAudio",
    DisplayName = "System Audio",
    Category = NodeCategory.Action,
    Description = "Controls speaker volume and microphone mute state",
    Color = "#22C55E")]
public sealed class SystemAudioNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.systemAudio",
        DisplayName = "System Audio",
        Category = NodeCategory.Action,
        Description = "Controls speaker volume and microphone mute state",
        Color = "#22C55E",
        InputPorts = FlowInput(),
        OutputPorts = HardwareOutputs(),
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "operation",
                Name = "Operation",
                Type = PropertyType.Dropdown,
                DefaultValue = "increaseOutputVolume",
                Description = "Audio operation to execute",
                Options = new[]
                {
                    "getState",
                    "setOutputVolume",
                    "increaseOutputVolume",
                    "decreaseOutputVolume",
                    "muteOutput",
                    "unmuteOutput",
                    "toggleOutputMute",
                    "setMicrophoneVolume",
                    "muteMicrophone",
                    "unmuteMicrophone",
                    "toggleMicrophoneMute"
                }
            },
            new() { Id = "percent", Name = "Percent", Type = PropertyType.Integer, DefaultValue = 50, Description = "Absolute volume percent for set operations" },
            new() { Id = "stepPercent", Name = "Step Percent", Type = PropertyType.Integer, DefaultValue = 5, Description = "Relative volume step for increase/decrease operations" },
            new() { Id = "storeSummaryInVariable", Name = "Store Summary In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the audio state summary" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var operation = NodeValueHelper.GetString(_properties, "operation", "increaseOutputVolume").Trim();
            var percent = NodeValueHelper.GetInt(_properties, "percent", 50);
            var step = Math.Max(1, NodeValueHelper.GetInt(_properties, "stepPercent", 5));

            _ = operation.ToLowerInvariant() switch
            {
                "setoutputvolume" => AudioEndpointController.SetVolume(AudioEndpointKind.Output, percent),
                "increaseoutputvolume" => AudioEndpointController.AdjustVolume(AudioEndpointKind.Output, step),
                "decreaseoutputvolume" => AudioEndpointController.AdjustVolume(AudioEndpointKind.Output, -step),
                "muteoutput" => AudioEndpointController.SetMute(AudioEndpointKind.Output, true),
                "unmuteoutput" => AudioEndpointController.SetMute(AudioEndpointKind.Output, false),
                "toggleoutputmute" => AudioEndpointController.ToggleMute(AudioEndpointKind.Output),
                "setmicrophonevolume" => AudioEndpointController.SetVolume(AudioEndpointKind.Microphone, percent),
                "mutemicrophone" => AudioEndpointController.SetMute(AudioEndpointKind.Microphone, true),
                "unmutemicrophone" => AudioEndpointController.SetMute(AudioEndpointKind.Microphone, false),
                "togglemicrophonemute" => AudioEndpointController.ToggleMute(AudioEndpointKind.Microphone),
                "getstate" => AudioEndpointController.GetState(AudioEndpointKind.Output),
                _ => throw new InvalidOperationException($"Unknown audio operation: {operation}")
            };

            var outputState = AudioEndpointController.GetState(AudioEndpointKind.Output);
            var microphoneState = AudioEndpointController.GetState(AudioEndpointKind.Microphone);
            var summary = $"Output {outputState.VolumePercent}% muted={outputState.Muted}; microphone {microphoneState.VolumePercent}% muted={microphoneState.Muted}";
            NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeSummaryInVariable", ""), summary);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["outputVolumePercent"] = outputState.VolumePercent,
                ["outputMuted"] = outputState.Muted,
                ["microphoneVolumePercent"] = microphoneState.VolumePercent,
                ["microphoneMuted"] = microphoneState.Muted,
                ["summary"] = summary
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["error"] = ex.Message
            }));
        }
    }

    internal static List<PortDefinition> FlowInput() => new()
    {
        new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
    };

    internal static List<PortDefinition> HardwareOutputs() => new()
    {
        new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
        new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
        new() { Id = "summary", Name = "Summary", DataType = PortDataType.String }
    };
}

[NodeInfo(
    TypeId = "action.hardwareDevice",
    DisplayName = "Hardware Device",
    Category = NodeCategory.Action,
    Description = "Enables, disables, or lists camera, microphone, and Wi-Fi devices",
    Color = "#22C55E")]
public sealed class HardwareDeviceNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.hardwareDevice",
        DisplayName = "Hardware Device",
        Category = NodeCategory.Action,
        Description = "Enables, disables, or lists camera, microphone, and Wi-Fi devices",
        Color = "#22C55E",
        InputPorts = SystemAudioNode.FlowInput(),
        OutputPorts = SystemAudioNode.HardwareOutputs(),
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "operation",
                Name = "Operation",
                Type = PropertyType.Dropdown,
                DefaultValue = "listDevices",
                Description = "Device operation. Changes may require administrator privileges.",
                Options = new[] { "listDevices", "enableCamera", "disableCamera", "enableMicrophoneDevice", "disableMicrophoneDevice", "enableWifi", "disableWifi" }
            },
            new() { Id = "nameFilter", Name = "Name Filter", Type = PropertyType.String, DefaultValue = "", Description = "Optional device name or InstanceId regex filter" },
            new() { Id = "allowSystemChanges", Name = "Allow System Changes", Type = PropertyType.Boolean, DefaultValue = false, Description = "Required for enable/disable operations" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var operation = NodeValueHelper.GetString(_properties, "operation", "listDevices").Trim();
        var filter = NodeValueHelper.ResolveTemplateProperty(context, _properties, "nameFilter", "");
        var isChange = !string.Equals(operation, "listDevices", StringComparison.OrdinalIgnoreCase);

        if (isChange && !NodeValueHelper.GetBool(_properties, "allowSystemChanges", false))
            return NodeResult.Fail("Hardware device changes are blocked. Set allowSystemChanges=true after reviewing the target device.");

        try
        {
            var result = operation.ToLowerInvariant() switch
            {
                "listdevices" => await HardwareDeviceController.ListDevicesAsync(filter, ct).ConfigureAwait(false),
                "enablecamera" => await HardwareDeviceController.SetCameraEnabledAsync(true, filter, ct).ConfigureAwait(false),
                "disablecamera" => await HardwareDeviceController.SetCameraEnabledAsync(false, filter, ct).ConfigureAwait(false),
                "enablemicrophonedevice" => await HardwareDeviceController.SetMicrophoneDeviceEnabledAsync(true, filter, ct).ConfigureAwait(false),
                "disablemicrophonedevice" => await HardwareDeviceController.SetMicrophoneDeviceEnabledAsync(false, filter, ct).ConfigureAwait(false),
                "enablewifi" => await HardwareDeviceController.SetWifiEnabledAsync(true, filter, ct).ConfigureAwait(false),
                "disablewifi" => await HardwareDeviceController.SetWifiEnabledAsync(false, filter, ct).ConfigureAwait(false),
                _ => new HardwareDeviceCommandResult(1, "", $"Unknown hardware operation: {operation}", operation)
            };

            var outputs = new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["exitCode"] = result.ExitCode,
                ["stdout"] = result.Stdout,
                ["stderr"] = result.Stderr,
                ["summary"] = result.CommandSummary
            };

            return result.ExitCode == 0
                ? NodeResult.Ok("out", outputs)
                : NodeResult.Ok("error", outputs);
        }
        catch (Exception ex)
        {
            return NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["error"] = ex.Message
            });
        }
    }
}

[NodeInfo(
    TypeId = "action.systemPower",
    DisplayName = "System Power",
    Category = NodeCategory.Action,
    Description = "Locks, sleeps, hibernates, restarts, or shuts down the computer with safety guards",
    Color = "#22C55E")]
public sealed class SystemPowerNode : IActionNode
{
    private const string RequiredSafetyPhrase = "CONFIRM";
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.systemPower",
        DisplayName = "System Power",
        Category = NodeCategory.Action,
        Description = "Locks, sleeps, hibernates, restarts, or shuts down the computer with safety guards",
        Color = "#22C55E",
        InputPorts = SystemAudioNode.FlowInput(),
        OutputPorts = SystemAudioNode.HardwareOutputs(),
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "operation",
                Name = "Operation",
                Type = PropertyType.Dropdown,
                DefaultValue = "lock",
                Description = "Power operation to request",
                Options = new[] { "lock", "sleep", "hibernate", "shutdown", "restart", "logoff", "cancelShutdown" }
            },
            new() { Id = "delaySeconds", Name = "Delay Seconds", Type = PropertyType.Integer, DefaultValue = 30, Description = "Delay for shutdown/restart operations" },
            new() { Id = "forceApps", Name = "Force Apps", Type = PropertyType.Boolean, DefaultValue = false, Description = "Force closing applications for shutdown/restart/logoff" },
            new() { Id = "safetyPhrase", Name = "Safety Phrase", Type = PropertyType.String, DefaultValue = "", Description = "Type CONFIRM for sleep, hibernate, shutdown, restart, or logoff" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var operation = NodeValueHelper.GetString(_properties, "operation", "lock").Trim();
        if (RequiresSafetyPhrase(operation) && !string.Equals(NodeValueHelper.GetString(_properties, "safetyPhrase", ""), RequiredSafetyPhrase, StringComparison.Ordinal))
            return NodeResult.Fail($"System power operation '{operation}' is blocked. Type {RequiredSafetyPhrase} in safetyPhrase after reviewing this automation.");

        var result = await SystemPowerController.ExecuteAsync(
            operation,
            NodeValueHelper.GetInt(_properties, "delaySeconds", 30),
            NodeValueHelper.GetBool(_properties, "forceApps", false),
            ct).ConfigureAwait(false);

        var outputs = new Dictionary<string, object?>
        {
            ["operation"] = operation,
            ["summary"] = result.Message
        };

        return result.Success ? NodeResult.Ok("out", outputs) : NodeResult.Ok("error", outputs);
    }

    private static bool RequiresSafetyPhrase(string operation)
    {
        return operation.Trim().ToLowerInvariant() is "sleep" or "hibernate" or "shutdown" or "restart" or "logoff";
    }
}

[NodeInfo(
    TypeId = "action.displaySettings",
    DisplayName = "Display Settings",
    Category = NodeCategory.Action,
    Description = "Describes monitors or changes resolution, rotation, and screen layout",
    Color = "#22C55E")]
public sealed class DisplaySettingsNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.displaySettings",
        DisplayName = "Display Settings",
        Category = NodeCategory.Action,
        Description = "Describes monitors or changes resolution, rotation, and screen layout",
        Color = "#22C55E",
        InputPorts = SystemAudioNode.FlowInput(),
        OutputPorts = SystemAudioNode.HardwareOutputs(),
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "operation",
                Name = "Operation",
                Type = PropertyType.Dropdown,
                DefaultValue = "describe",
                Description = "Display operation to execute",
                Options = new[] { "describe", "setResolution", "setOrientation", "setPosition", "setResolutionAndLayout" }
            },
            new() { Id = "deviceName", Name = "Device Name", Type = PropertyType.String, DefaultValue = "", Description = "Windows display device name. Empty means primary display." },
            new() { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 1920, Description = "Target pixel width" },
            new() { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 1080, Description = "Target pixel height" },
            new() { Id = "refreshRate", Name = "Refresh Rate", Type = PropertyType.Integer, DefaultValue = 0, Description = "Optional refresh rate. 0 keeps the current rate." },
            new() { Id = "orientation", Name = "Orientation", Type = PropertyType.Dropdown, DefaultValue = "landscape", Description = "Display rotation", Options = new[] { "landscape", "portrait", "landscapeFlipped", "portraitFlipped" } },
            new() { Id = "positionX", Name = "Position X", Type = PropertyType.Integer, DefaultValue = 0, Description = "Monitor layout X position" },
            new() { Id = "positionY", Name = "Position Y", Type = PropertyType.Integer, DefaultValue = 0, Description = "Monitor layout Y position" },
            new() { Id = "allowSystemChanges", Name = "Allow System Changes", Type = PropertyType.Boolean, DefaultValue = false, Description = "Required before changing display settings" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var operation = NodeValueHelper.GetString(_properties, "operation", "describe").Trim();

        if (string.Equals(operation, "describe", StringComparison.OrdinalIgnoreCase))
        {
            var displaysJson = DisplaySettingsController.DescribeDisplaysJson();
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["summary"] = "display layout described",
                ["displaysJson"] = displaysJson
            }));
        }

        if (!NodeValueHelper.GetBool(_properties, "allowSystemChanges", false))
            return Task.FromResult(NodeResult.Fail("Display changes are blocked. Set allowSystemChanges=true after reviewing the target monitor and resolution."));

        int? width = null;
        int? height = null;
        int? refreshRate = null;
        int? orientation = null;
        int? positionX = null;
        int? positionY = null;

        var normalized = operation.ToLowerInvariant();
        if (normalized is "setresolution" or "setresolutionandlayout")
        {
            width = NodeValueHelper.GetInt(_properties, "width", 1920);
            height = NodeValueHelper.GetInt(_properties, "height", 1080);
            var rate = NodeValueHelper.GetInt(_properties, "refreshRate", 0);
            refreshRate = rate > 0 ? rate : null;
        }

        if (normalized is "setorientation" or "setresolutionandlayout")
            orientation = ParseOrientation(NodeValueHelper.GetString(_properties, "orientation", "landscape"));

        if (normalized is "setposition" or "setresolutionandlayout")
        {
            positionX = NodeValueHelper.GetInt(_properties, "positionX", 0);
            positionY = NodeValueHelper.GetInt(_properties, "positionY", 0);
        }

        var result = DisplaySettingsController.ChangeDisplay(
            NodeValueHelper.ResolveTemplateProperty(context, _properties, "deviceName", ""),
            width,
            height,
            refreshRate,
            orientation,
            positionX,
            positionY);

        var outputs = new Dictionary<string, object?>
        {
            ["summary"] = result.Message,
            ["code"] = result.Code,
            ["displaysJson"] = DisplaySettingsController.DescribeDisplaysJson()
        };

        return Task.FromResult(result.Success ? NodeResult.Ok("out", outputs) : NodeResult.Ok("error", outputs));
    }

    private static int ParseOrientation(string orientation)
    {
        return orientation.Trim().ToLowerInvariant() switch
        {
            "portrait" => 1,
            "landscapeflipped" => 2,
            "portraitflipped" => 3,
            _ => 0
        };
    }
}
