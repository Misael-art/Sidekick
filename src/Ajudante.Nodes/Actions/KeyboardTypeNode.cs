using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Input;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.keyboardType",
    DisplayName = "Keyboard Type",
    Category = NodeCategory.Action,
    Description = "Types text using the keyboard",
    Color = "#22C55E")]
public class KeyboardTypeNode : IActionNode
{
    private string _text = "";
    private int _delayBetweenKeys;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.keyboardType",
        DisplayName = "Keyboard Type",
        Category = NodeCategory.Action,
        Description = "Types text using the keyboard",
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
                Id = "text",
                Name = "Text",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "The text to type (supports {{variable}} templates)"
            },
            new()
            {
                Id = "delayBetweenKeys",
                Name = "Delay Between Keys (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Delay in milliseconds between each keystroke"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("text", out var t) && t is string text)
            _text = text;
        if (properties.TryGetValue("delayBetweenKeys", out var d))
            _delayBetweenKeys = Convert.ToInt32(d);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var resolvedText = context.ResolveTemplate(_text);

            if (_delayBetweenKeys > 0)
            {
                foreach (var ch in resolvedText)
                {
                    ct.ThrowIfCancellationRequested();
                    KeyboardSimulator.TypeText(ch.ToString());
                    await Task.Delay(_delayBetweenKeys, ct);
                }
            }
            else
            {
                KeyboardSimulator.TypeText(resolvedText);
            }

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["typedText"] = resolvedText,
                ["length"] = resolvedText.Length
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Keyboard type failed: {ex.Message}");
        }
    }
}
