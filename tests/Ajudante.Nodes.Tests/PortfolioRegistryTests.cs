using Ajudante.Core.Registry;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Logic;
using Ajudante.Nodes.Triggers;

namespace Ajudante.Nodes.Tests;

public class PortfolioRegistryTests
{
    [Fact]
    public void ScanAssembly_RegistersNewPortfolioNodes()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(TextTemplateNode).Assembly);

        var expectedTypeIds = new[]
        {
            "trigger.manualStart",
            "action.logMessage",
            "logic.textTemplate",
            "action.readClipboard",
            "action.writeClipboard",
            "action.readFile",
            "action.writeFile",
            "action.listFiles",
            "action.moveFile",
            "action.jsonExtract",
            "action.readCsv",
            "action.writeCsv",
            "action.httpRequest",
            "logic.filterTextLines",
            "logic.retryFlow",
            "action.browserOpenUrl",
            "action.browserClick",
            "action.browserType",
            "action.browserWaitElement",
            "action.browserExtractText",
            "action.sendEmail",
            "action.showNotification"
        };

        var definitions = registry.GetAllDefinitions();
        foreach (var typeId in expectedTypeIds)
            Assert.Contains(definitions, d => d.TypeId == typeId);
    }

    [Fact]
    public void CreateInstance_CanInstantiateRepresentativeNewNodes()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(TextTemplateNode).Assembly);

        Assert.IsType<ManualStartTriggerNode>(registry.CreateInstance("trigger.manualStart"));
        Assert.IsType<HttpRequestNode>(registry.CreateInstance("action.httpRequest"));
        Assert.IsType<RetryFlowNode>(registry.CreateInstance("logic.retryFlow"));
        Assert.IsType<SendEmailNode>(registry.CreateInstance("action.sendEmail"));
    }
}
