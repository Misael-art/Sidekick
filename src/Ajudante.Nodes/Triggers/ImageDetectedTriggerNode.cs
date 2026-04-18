using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Platform.Screen;

namespace Ajudante.Nodes.Triggers;

[NodeInfo(
    TypeId = "trigger.imageDetected",
    DisplayName = "Image Detected",
    Category = NodeCategory.Trigger,
    Description = "Fires when a template image is detected on screen",
    Color = "#EF4444")]
public class ImageDetectedTriggerNode : ITriggerNode
{
    private CancellationTokenSource? _pollCts;
    private byte[]? _templateImage;
    private double _threshold = 0.8;
    private int _interval = 1000;

    public string Id { get; set; } = "";
    public event Action<TriggerEventArgs>? Triggered;

    public NodeDefinition Definition => new()
    {
        TypeId = "trigger.imageDetected",
        DisplayName = "Image Detected",
        Category = NodeCategory.Trigger,
        Description = "Fires when a template image is detected on screen",
        Color = "#EF4444",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow },
            new() { Id = "x", Name = "X", DataType = PortDataType.Number },
            new() { Id = "y", Name = "Y", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "templateImage",
                Name = "Template Image",
                Type = PropertyType.ImageTemplate,
                Description = "The image to search for on screen"
            },
            new()
            {
                Id = "threshold",
                Name = "Threshold",
                Type = PropertyType.Float,
                DefaultValue = 0.8,
                Description = "Match confidence threshold (0.0 - 1.0)"
            },
            new()
            {
                Id = "interval",
                Name = "Poll Interval (ms)",
                Type = PropertyType.Integer,
                DefaultValue = 1000,
                Description = "How often to scan the screen in milliseconds"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("templateImage", out var img))
        {
            if (img is byte[] bytes)
                _templateImage = bytes;
            else if (img is string base64 && !string.IsNullOrEmpty(base64))
                _templateImage = Convert.FromBase64String(base64);
        }

        if (properties.TryGetValue("threshold", out var th))
            _threshold = Convert.ToDouble(th);

        if (properties.TryGetValue("interval", out var iv))
            _interval = Convert.ToInt32(iv);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("triggered"));
    }

    public Task StartWatchingAsync(CancellationToken ct)
    {
        if (_templateImage == null || _templateImage.Length == 0)
            return Task.CompletedTask;

        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _pollCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var match = TemplateMatching.FindOnScreen(_templateImage, _threshold);
                    if (match != null)
                    {
                        Triggered?.Invoke(new TriggerEventArgs
                        {
                            Data = new Dictionary<string, object?>
                            {
                                ["x"] = match.X,
                                ["y"] = match.Y,
                                ["width"] = match.Width,
                                ["height"] = match.Height,
                                ["confidence"] = match.Confidence
                            },
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    await Task.Delay(_interval, token);
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
