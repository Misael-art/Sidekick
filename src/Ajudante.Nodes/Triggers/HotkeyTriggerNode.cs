using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.hotkey",
    DisplayName = "Hotkey Trigger",
    Category = NodeCategory.Trigger,
    Description = "Fires when a global hotkey combination is pressed",
    Color = "#EF4444")]
public class HotkeyTriggerNode : ITriggerNode, IDisposable
{
    private GlobalHotkeyManager? _hotkeyManager;
    private int _hotkeyId;
    private string _key = "F1";
    private string _modifiers = "None";

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.hotkey",
        DisplayName = "Hotkey Trigger",
        Category = NodeCategory.Trigger,
        Description = "Fires when a global hotkey combination is pressed",
        Color = "#EF4444",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "key",
                Name = "Key",
                Type = PropertyType.Hotkey,
                DefaultValue = "F1",
                Description = "The key to listen for"
            },
            new()
            {
                Id = "modifiers",
                Name = "Modifiers",
                Type = PropertyType.Dropdown,
                DefaultValue = "None",
                Description = "Key modifiers",
                Options = new[] { "None", "Ctrl", "Shift", "Alt", "Win" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("key", out var key) && key is string k)
            _key = k;
        if (properties.TryGetValue("modifiers", out var mod) && mod is string m)
            _modifiers = m;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_hotkeyId != 0)
        {
            return Task.CompletedTask;
        }

        _hotkeyManager ??= new GlobalHotkeyManager();

        var modifiers = ParseModifiers(_modifiers);
        var vk = ParseKey(_key);

        _hotkeyId = _hotkeyManager.RegisterHotkey(modifiers, vk, () =>
        {
            Triggered?.Invoke(new TriggerEventArgs
            {
                Data = new Dictionary<string, object?>
                {
                    ["key"] = _key,
                    ["modifiers"] = _modifiers
                },
                Timestamp = DateTime.UtcNow
            });
        });

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        if (_hotkeyManager != null)
        {
            if (_hotkeyId != 0)
            {
                _hotkeyManager.UnregisterHotkey(_hotkeyId);
                _hotkeyId = 0;
            }
            _hotkeyManager.UnregisterAll();
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopWatchingAsync().GetAwaiter().GetResult();
        _hotkeyManager?.Dispose();
        _hotkeyManager = null;
    }

    private static HotkeyModifiers ParseModifiers(string modifier) => modifier switch
    {
        "Ctrl" => HotkeyModifiers.Ctrl,
        "Shift" => HotkeyModifiers.Shift,
        "Alt" => HotkeyModifiers.Alt,
        "Win" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None
    };

    private static VirtualKey ParseKey(string key)
    {
        if (Enum.TryParse<VirtualKey>($"VK_{key.ToUpperInvariant()}", out var vk))
            return vk;
        if (Enum.TryParse<VirtualKey>(key, out var vk2))
            return vk2;
        return VirtualKey.VK_F1;
    }
}
