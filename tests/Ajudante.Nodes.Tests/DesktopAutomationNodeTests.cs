using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Triggers;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Tests;

public class DesktopAutomationNodeTests
{
    [Fact]
    public void DesktopNodes_ExposeProcessScopedSelectorProperties()
    {
        var nodes = new[]
        {
            new DesktopWaitElementNode().Definition,
            new DesktopClickElementNode().Definition,
            new DesktopReadElementTextNode().Definition
        };

        foreach (var definition in nodes)
        {
            Assert.Contains(definition.Properties, p => p.Id == "processName");
            Assert.Contains(definition.Properties, p => p.Id == "processPath");
            Assert.Contains(definition.Properties, p => p.Id == "windowTitleMatch");
            Assert.Contains(definition.OutputPorts, p => p.Id == "notFound");
        }
    }

    [Fact]
    public void DesktopElementAppearedTrigger_HasLoopGuards()
    {
        var definition = new DesktopElementAppearedTriggerNode().Definition;

        Assert.Equal("trigger.desktopElementAppeared", definition.TypeId);
        Assert.Contains(definition.Properties, p => p.Id == "cooldownMs");
        Assert.Contains(definition.Properties, p => p.Id == "debounceMs");
        Assert.Contains(definition.Properties, p => p.Id == "maxRepeat");
        Assert.Contains(definition.Properties, p => p.Id == "processPath");
    }

    [Fact]
    public void VisualFallbackAndSchedulerNodes_ArePublicProductNodes()
    {
        Assert.Equal("action.clickImageMatch", new ClickImageMatchNode().Definition.TypeId);
        Assert.Equal("trigger.scheduleTime", new ScheduleTimeTriggerNode().Definition.TypeId);
        Assert.Equal("trigger.interval", new IntervalTriggerNode().Definition.TypeId);
        Assert.Equal("trigger.desktopElementTextChanged", new DesktopElementTextChangedTriggerNode().Definition.TypeId);
        Assert.Equal("trigger.processEvent", new ProcessEventTriggerNode().Definition.TypeId);
        Assert.Equal("action.windowControl", new WindowControlNode().Definition.TypeId);
        Assert.Equal("action.waitProcess", new WaitProcessNode().Definition.TypeId);
    }

    [Fact]
    public void OverlayAndConsoleNodes_ArePublicProductNodes()
    {
        Assert.Equal("action.overlayColor", new OverlayColorNode().Definition.TypeId);
        Assert.Equal("action.overlayImage", new OverlayImageNode().Definition.TypeId);
        Assert.Equal("action.overlayText", new OverlayTextNode().Definition.TypeId);
        Assert.Equal("action.consoleSetDirectory", new ConsoleSetDirectoryNode().Definition.TypeId);
        Assert.Equal("action.consoleCommand", new ConsoleCommandNode().Definition.TypeId);

        Assert.Contains(new OverlayTextNode().Definition.Properties, p => p.Id == "motion");
        Assert.Contains(new OverlayImageNode().Definition.Properties, p => p.Id == "fit");
        Assert.Contains(new OverlayColorNode().Definition.Properties, p => p.Id == "durationMs");
        Assert.Contains(new ConsoleCommandNode().Definition.OutputPorts, p => p.Id == "error");
        Assert.Contains(new ConsoleCommandNode().Definition.Properties, p => p.Id == "workingDirectory");
    }

    [Fact]
    public void HardwareNodes_ArePublicProductNodesWithSafetyGuards()
    {
        Assert.Equal("action.systemAudio", new SystemAudioNode().Definition.TypeId);
        Assert.Equal("action.hardwareDevice", new HardwareDeviceNode().Definition.TypeId);
        Assert.Equal("action.systemPower", new SystemPowerNode().Definition.TypeId);
        Assert.Equal("action.displaySettings", new DisplaySettingsNode().Definition.TypeId);

        Assert.Contains(new SystemAudioNode().Definition.Properties, p => p.Id == "operation");
        Assert.Contains(new HardwareDeviceNode().Definition.Properties, p => p.Id == "allowSystemChanges");
        Assert.Contains(new SystemPowerNode().Definition.Properties, p => p.Id == "safetyPhrase");
        Assert.Contains(new DisplaySettingsNode().Definition.Properties, p => p.Id == "allowSystemChanges");
    }

    [Fact]
    public async Task HardwareDeviceNode_BlocksDeviceChangesWithoutExplicitPermission()
    {
        var node = new HardwareDeviceNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["operation"] = "disableWifi",
            ["allowSystemChanges"] = false
        });

        var context = new FlowExecutionContext(new Flow { Id = "flow-hardware", Name = "Hardware Flow" }, CancellationToken.None);
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("allowSystemChanges", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SystemPowerNode_BlocksShutdownWithoutSafetyPhrase()
    {
        var node = new SystemPowerNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["operation"] = "shutdown",
            ["safetyPhrase"] = ""
        });

        var context = new FlowExecutionContext(new Flow { Id = "flow-power", Name = "Power Flow" }, CancellationToken.None);
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("CONFIRM", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsoleSetDirectory_StoresPwdVariableAndOutput()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sidekick-console-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var node = new ConsoleSetDirectoryNode();
            node.Configure(new Dictionary<string, object?>
            {
                ["workingDirectory"] = tempDirectory,
                ["variableName"] = "pwd",
                ["createIfMissing"] = false
            });

            var context = new FlowExecutionContext(new Flow { Id = "flow-console", Name = "Console Flow" }, CancellationToken.None);
            var result = await node.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("out", result.OutputPort);
            Assert.Equal(tempDirectory, context.GetVariable<string>("pwd"));
            Assert.Equal(tempDirectory, result.Outputs["workingDirectory"]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("", AutomationElementLocator.TitleMatch.Equals)]
    [InlineData("equals", AutomationElementLocator.TitleMatch.Equals)]
    [InlineData("contains", AutomationElementLocator.TitleMatch.Contains)]
    [InlineData("regex", AutomationElementLocator.TitleMatch.Regex)]
    [InlineData("garbage", AutomationElementLocator.TitleMatch.Equals)]
    public void TitleMatchParser_DefaultsSafely(string input, AutomationElementLocator.TitleMatch expected)
    {
        Assert.Equal(expected, AutomationElementLocator.ParseTitleMatch(input));
    }

    [Theory]
    [InlineData("Trae - project", "trae", AutomationElementLocator.TitleMatch.Contains, true)]
    [InlineData("Trae - project", "Trae - project", AutomationElementLocator.TitleMatch.Equals, true)]
    [InlineData("Trae - project", "Other", AutomationElementLocator.TitleMatch.Equals, false)]
    [InlineData("Trae - project", "Trae.*project", AutomationElementLocator.TitleMatch.Regex, true)]
    [InlineData("Trae - project", "[bad", AutomationElementLocator.TitleMatch.Regex, false)]
    public void TitleMatches_UsesRequestedMode(string candidate, string pattern, AutomationElementLocator.TitleMatch mode, bool expected)
    {
        Assert.Equal(expected, AutomationElementLocator.TitleMatches(candidate, pattern, mode));
    }
}
