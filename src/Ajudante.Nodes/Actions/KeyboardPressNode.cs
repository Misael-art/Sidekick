using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.keyboardPress",
    DisplayName = "Keyboard Press",
    Category = NodeCategory.Action,
    Description = "Presses a key or key combination",
    Color = "#22C55E")]
public class KeyboardPressNode : IActionNode
{
    private string _key = "Return";
    private string _modifiers = "None";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.keyboardPress",
        DisplayName = "Keyboard Press",
        Category = NodeCategory.Action,
        Description = "Presses a key or key combination",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "key",
                Name = "Key",
                Type = PropertyType.Dropdown,
                DefaultValue = "Return",
                Description = "The key to press",
                Options = new[]
                {
                    "Return", "Tab", "Escape", "Space", "Backspace", "Delete",
                    "Up", "Down", "Left", "Right",
                    "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                    "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
                    "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
                }
            },
            new()
            {
                Id = "modifiers",
                Name = "Modifiers",
                Type = PropertyType.Dropdown,
                DefaultValue = "None",
                Description = "Key modifier to hold while pressing",
                Options = new[] { "None", "Ctrl", "Shift", "Alt" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("key", out var k) && k is string key)
            _key = key;
        if (properties.TryGetValue("modifiers", out var m) && m is string mod)
            _modifiers = mod;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var vk = MapKeyToVirtualKey(_key);

            if (_modifiers != "None")
            {
                var modifierVk = _modifiers switch
                {
                    "Ctrl" => VirtualKey.VK_CONTROL,
                    "Shift" => VirtualKey.VK_SHIFT,
                    "Alt" => VirtualKey.VK_MENU,
                    _ => throw new InvalidOperationException($"Unknown modifier: {_modifiers}")
                };
                KeyboardSimulator.PressCombo(modifierVk, vk);
            }
            else
            {
                KeyboardSimulator.PressKey(vk);
            }

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["key"] = _key,
                ["modifiers"] = _modifiers
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Keyboard press failed: {ex.Message}"));
        }
    }

    private static VirtualKey MapKeyToVirtualKey(string key) => key switch
    {
        "Return" => VirtualKey.VK_RETURN,
        "Tab" => VirtualKey.VK_TAB,
        "F1" => VirtualKey.VK_F1,
        "F2" => VirtualKey.VK_F2,
        "F3" => VirtualKey.VK_F3,
        "F4" => VirtualKey.VK_F4,
        "F5" => VirtualKey.VK_F5,
        "F6" => VirtualKey.VK_F6,
        "F7" => VirtualKey.VK_F7,
        "F8" => VirtualKey.VK_F8,
        "F9" => VirtualKey.VK_F9,
        "F10" => VirtualKey.VK_F10,
        "F11" => VirtualKey.VK_F11,
        "F12" => VirtualKey.VK_F12,
        _ when key.Length == 1 && char.IsLetter(key[0]) =>
            Enum.Parse<VirtualKey>($"VK_{key.ToUpperInvariant()}"),
        _ => Enum.TryParse<VirtualKey>($"VK_{key.ToUpperInvariant()}", out var vk) ? vk : VirtualKey.VK_RETURN
    };
}
