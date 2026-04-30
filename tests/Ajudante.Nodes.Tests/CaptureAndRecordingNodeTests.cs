using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class CaptureAndRecordingNodeTests
{
    [Fact]
    public void CaptureScreenshotNode_DefinitionIncludesRequiredProductContract()
    {
        var definition = new CaptureScreenshotNode().Definition;

        Assert.Equal("action.captureScreenshot", definition.TypeId);
        Assert.Contains(definition.Properties, property => property.Id == "target");
        Assert.Contains(definition.Properties, property => property.Id == "outputFolder");
        Assert.Contains(definition.Properties, property => property.Id == "filenameTemplate");
        Assert.Contains(definition.Properties, property => property.Id == "format");
        Assert.Contains(definition.Properties, property => property.Id == "windowTitle");
        Assert.Contains(definition.Properties, property => property.Id == "processPath");
        Assert.Contains(definition.Properties, property => property.Id == "effect");
        Assert.Contains(definition.OutputPorts, port => port.Id == "filePath");
        Assert.Contains(definition.OutputPorts, port => port.Id == "width");
        Assert.Contains(definition.OutputPorts, port => port.Id == "height");
        Assert.Contains(definition.OutputPorts, port => port.Id == "target");
        Assert.Contains(definition.OutputPorts, port => port.Id == "error");
    }

    [Fact]
    public async Task CaptureScreenshotNode_FailsFastOnUnsupportedImageFormat()
    {
        var node = new CaptureScreenshotNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["target"] = "region",
            ["x"] = 0,
            ["y"] = 0,
            ["width"] = 10,
            ["height"] = 10,
            ["format"] = "gif"
        });

        var context = new FlowExecutionContext(new Flow { Id = "capture", Name = "Capture" }, CancellationToken.None);
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("format", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordDesktopNode_DefinitionIncludesRequiredProductContract()
    {
        var definition = new RecordDesktopNode().Definition;

        Assert.Equal("action.recordDesktop", definition.TypeId);
        Assert.Contains(definition.Properties, property => property.Id == "target");
        Assert.Contains(definition.Properties, property => property.Id == "durationMs");
        Assert.Contains(definition.Properties, property => property.Id == "fps");
        Assert.Contains(definition.Properties, property => property.Id == "countdownMs");
        Assert.Contains(definition.Properties, property => property.Id == "showRecordingIndicator");
        Assert.Contains(definition.Properties, property => property.Id == "maxFileSizeMb");
        Assert.Contains(definition.OutputPorts, port => port.Id == "filePath");
        Assert.Contains(definition.OutputPorts, port => port.Id == "durationMs");
        Assert.Contains(definition.OutputPorts, port => port.Id == "framesWritten");
        Assert.Contains(definition.OutputPorts, port => port.Id == "fps");
        Assert.Contains(definition.OutputPorts, port => port.Id == "target");
        Assert.Contains(definition.OutputPorts, port => port.Id == "error");
    }

    [Fact]
    public async Task RecordDesktopNode_FailsFastOnInvalidFps()
    {
        var node = new RecordDesktopNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["durationMs"] = 1000,
            ["fps"] = 0
        });

        var context = new FlowExecutionContext(new Flow { Id = "record-desktop", Name = "Record Desktop" }, CancellationToken.None);
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("fps", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordCameraNode_DefinitionIncludesRequiredProductContract()
    {
        var definition = new RecordCameraNode().Definition;

        Assert.Equal("action.recordCamera", definition.TypeId);
        Assert.Contains(definition.Properties, property => property.Id == "cameraIndex");
        Assert.Contains(definition.Properties, property => property.Id == "cameraNameFilter");
        Assert.Contains(definition.Properties, property => property.Id == "width");
        Assert.Contains(definition.Properties, property => property.Id == "height");
        Assert.Contains(definition.Properties, property => property.Id == "fps");
        Assert.Contains(definition.Properties, property => property.Id == "durationMs");
        Assert.Contains(definition.Properties, property => property.Id == "overlayTimestamp");
        Assert.Contains(definition.OutputPorts, port => port.Id == "filePath");
        Assert.Contains(definition.OutputPorts, port => port.Id == "cameraName");
        Assert.Contains(definition.OutputPorts, port => port.Id == "width");
        Assert.Contains(definition.OutputPorts, port => port.Id == "height");
        Assert.Contains(definition.OutputPorts, port => port.Id == "framesWritten");
        Assert.Contains(definition.OutputPorts, port => port.Id == "error");
    }

    [Fact]
    public async Task RecordCameraNode_FailsFastOnInvalidDuration()
    {
        var node = new RecordCameraNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["durationMs"] = 0
        });

        var context = new FlowExecutionContext(new Flow { Id = "record-camera", Name = "Record Camera" }, CancellationToken.None);
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("duration", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConditionGroupNode_EvaluatesAnyAndAllWithBasicOperators()
    {
        var context = new FlowExecutionContext(
            new Flow
            {
                Id = "flow-condition",
                Name = "Condition Group",
                Variables = new List<FlowVariable>
                {
                    new() { Name = "status", Type = VariableType.String, Default = "running" },
                    new() { Name = "attempts", Type = VariableType.Integer, Default = 5 }
                }
            },
            CancellationToken.None);

        var anyNode = new ConditionGroupNode();
        anyNode.Configure(new Dictionary<string, object?>
        {
            ["mode"] = "ANY",
            ["conditionsJson"] = """
            [
              { "left": "{{status}}", "operator": "equals", "right": "paused" },
              { "left": "{{attempts}}", "operator": "greater", "right": "3" }
            ]
            """
        });

        var anyResult = await anyNode.ExecuteAsync(context, CancellationToken.None);
        Assert.True(anyResult.Success);
        Assert.Equal("true", anyResult.OutputPort);

        var allNode = new ConditionGroupNode();
        allNode.Configure(new Dictionary<string, object?>
        {
            ["mode"] = "ALL",
            ["conditionsJson"] = """
            [
              { "left": "{{status}}", "operator": "contains", "right": "run" },
              { "left": "{{attempts}}", "operator": "less", "right": "3" }
            ]
            """
        });

        var allResult = await allNode.ExecuteAsync(context, CancellationToken.None);
        Assert.True(allResult.Success);
        Assert.Equal("false", allResult.OutputPort);
    }
}
