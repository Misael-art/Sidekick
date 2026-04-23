using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class LogMessageNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Log Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasExpectedPortsAndProperties()
    {
        var node = new LogMessageNode();

        Assert.Equal("action.logMessage", node.Definition.TypeId);
        Assert.Single(node.Definition.InputPorts);
        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Equal(3, node.Definition.Properties.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesTemplatesAndReturnsMessage()
    {
        var node = new LogMessageNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["message"] = "User {{user}} logged in",
            ["level"] = "warning"
        });

        var context = CreateContext();
        context.SetVariable("user", "Bob");

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("out", result.OutputPort);
        Assert.Equal("User Bob logged in", result.Outputs["message"]);
        Assert.Equal("warning", result.Outputs["level"]);
    }

    [Fact]
    public async Task ExecuteAsync_CanStoreResolvedMessageInVariable()
    {
        var node = new LogMessageNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["message"] = "Order {{id}} completed",
            ["storeInVariable"] = "lastLog"
        });

        var context = CreateContext();
        context.SetVariable("id", 123);

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Order 123 completed", context.GetVariable("lastLog"));
    }
}
