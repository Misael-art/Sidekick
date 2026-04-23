using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class RetryFlowNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Retry Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasRetryAndGiveUpPorts()
    {
        var node = new RetryFlowNode();

        Assert.Equal("logic.retryFlow", node.Definition.TypeId);
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "retry");
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "giveUp");
    }

    [Fact]
    public async Task ExecuteAsync_RoutesToRetryUntilLimit()
    {
        var node = new RetryFlowNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["counterVariable"] = "attempts",
            ["maxAttempts"] = 2
        });

        var context = CreateContext();

        var first = await node.ExecuteAsync(context, CancellationToken.None);
        var second = await node.ExecuteAsync(context, CancellationToken.None);
        var third = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("retry", first.OutputPort);
        Assert.Equal("retry", second.OutputPort);
        Assert.Equal("giveUp", third.OutputPort);
        Assert.Equal(3, context.GetVariable<int>("attempts"));
    }

    [Fact]
    public async Task ExecuteAsync_WaitsBeforeRetryWhenConfigured()
    {
        var node = new RetryFlowNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["counterVariable"] = "attempts",
            ["maxAttempts"] = 1,
            ["delayMs"] = 80
        });

        var context = CreateContext();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await node.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        Assert.Equal("retry", result.OutputPort);
        Assert.True(sw.ElapsedMilliseconds >= 60, $"Expected delay >= 60ms, got {sw.ElapsedMilliseconds}ms");
    }
}
