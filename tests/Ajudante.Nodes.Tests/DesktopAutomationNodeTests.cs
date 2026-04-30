using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Triggers;
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
