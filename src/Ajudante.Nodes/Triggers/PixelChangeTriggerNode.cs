using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Screen;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.pixelChange",
    DisplayName = "Pixel Change",
    Category = NodeCategory.Trigger,
    Description = "Fires when a pixel at a specific location changes color",
    Color = "#EF4444")]
public class PixelChangeTriggerNode : ITriggerNode
{
    private CancellationTokenSource? _pollCts;
    private int _x;
    private int _y;
    private int _interval = 500;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.pixelChange",
        DisplayName = "Pixel Change",
        Category = NodeCategory.Trigger,
        Description = "Fires when a pixel at a specific location changes color",
        Color = "#EF4444",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "oldColor", Name = "Old Color", DataType = PortDataType.String },
            new() { Id = "newColor", Name = "New Color", DataType = PortDataType.String },
            new() { Id = "x", Name = "X", DataType = PortDataType.Number },
            new() { Id = "y", Name = "Y", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "x",
                Name = "X",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "X coordinate of the pixel to watch"
            },
            new()
            {
                Id = "y",
                Name = "Y",
                Type = PropertyType.Integer,
                DefaultValue = 0,
                Description = "Y coordinate of the pixel to watch"
            },
            new()
            {
                Id = "interval",
                Name = "Poll Interval (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 500,
                Description = "How often to check the pixel in milliseconds"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("x", out var x))
            _x = Convert.ToInt32(x);
        if (properties.TryGetValue("y", out var y))
            _y = Convert.ToInt32(y);
        if (properties.TryGetValue("interval", out var iv))
            _interval = Convert.ToInt32(iv);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct)
    {
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _pollCts.Token;

        _ = Task.Run(async () =>
        {
            var lastColor = PixelReader.GetPixelColor(_x, _y);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, token);

                    var currentColor = PixelReader.GetPixelColor(_x, _y);
                    if (currentColor != lastColor)
                    {
                        var oldColor = lastColor;
                        lastColor = currentColor;

                        Triggered?.Invoke(new TriggerEventArgs
                        {
                            Data = new Dictionary<string, object?>
                            {
                                ["oldColor"] = oldColor.ToString(),
                                ["newColor"] = currentColor.ToString(),
                                ["x"] = _x,
                                ["y"] = _y
                            },
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(_interval, token);
                }
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
        return Task.CompletedTask;
    }
}
