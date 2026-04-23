using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Triggers;

namespace Ajudante.Nodes.Tests;

public class PortfolioNodeDefinitionTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Portfolio Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public async Task ManualStartTriggerNode_ExecutesSuccessfully()
    {
        var node = new ManualStartTriggerNode();
        var result = await node.ExecuteAsync(CreateContext(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("triggered", result.OutputPort);
    }

    [Fact]
    public void BrowserAndNotificationNodes_ExposeExpectedTypeIds()
    {
        Assert.Equal("action.browserOpenUrl", new BrowserOpenUrlNode().Definition.TypeId);
        Assert.Equal("action.browserClick", new BrowserClickNode().Definition.TypeId);
        Assert.Equal("action.browserType", new BrowserTypeNode().Definition.TypeId);
        Assert.Equal("action.browserWaitElement", new BrowserWaitElementNode().Definition.TypeId);
        Assert.Equal("action.browserExtractText", new BrowserExtractTextNode().Definition.TypeId);
        Assert.Equal("action.showNotification", new ShowNotificationNode().Definition.TypeId);
    }

    [Fact]
    public async Task BrowserNodes_ReturnFailWhenElementIsMissing()
    {
        var context = CreateContext();

        var waitNode = new BrowserWaitElementNode();
        waitNode.Configure(new Dictionary<string, object?>
        {
            ["windowTitle"] = "DefinitelyMissingWindow",
            ["timeoutMs"] = 0
        });

        var extractNode = new BrowserExtractTextNode();
        extractNode.Configure(new Dictionary<string, object?>
        {
            ["windowTitle"] = "DefinitelyMissingWindow",
            ["timeoutMs"] = 0
        });

        var clickNode = new BrowserClickNode();
        clickNode.Configure(new Dictionary<string, object?>
        {
            ["windowTitle"] = "DefinitelyMissingWindow",
            ["timeoutMs"] = 0
        });

        var typeNode = new BrowserTypeNode();
        typeNode.Configure(new Dictionary<string, object?>
        {
            ["windowTitle"] = "DefinitelyMissingWindow",
            ["timeoutMs"] = 0,
            ["text"] = "abc"
        });

        var waitResult = await waitNode.ExecuteAsync(context, CancellationToken.None);
        var extractResult = await extractNode.ExecuteAsync(context, CancellationToken.None);
        var clickResult = await clickNode.ExecuteAsync(context, CancellationToken.None);
        var typeResult = await typeNode.ExecuteAsync(context, CancellationToken.None);

        Assert.False(waitResult.Success);
        Assert.False(extractResult.Success);
        Assert.False(clickResult.Success);
        Assert.False(typeResult.Success);
    }
}
