using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class LoopNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new LoopNode();
        Assert.Equal("logic.loop", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasBodyAndDoneOutputPorts()
    {
        var node = new LoopNode();
        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "body");
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "done");
    }

    [Fact]
    public void Definition_HasCountAndDelayProperties()
    {
        var node = new LoopNode();
        Assert.Equal(2, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "count");
        Assert.Contains(node.Definition.Properties, p => p.Id == "delayBetween");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDone_AfterIterations()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 3,
            ["delayBetween"] = 0
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("done", result.OutputPort);
    }

    [Fact]
    public async Task ExecuteAsync_SetsLoopVariables()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 5,
            ["delayBetween"] = 0
        });

        var context = CreateContext();
        await node.ExecuteAsync(context, CancellationToken.None);

        // After all iterations, the loop index should be the last value
        Assert.Equal(4, context.GetVariable("loopIndex"));  // 0-indexed, last iteration
        Assert.Equal(5, context.GetVariable("loopIteration"));
        Assert.Equal(5, context.GetVariable("loopCount"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCorrectOutputs()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 3,
            ["delayBetween"] = 0
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(2, result.Outputs["loopIndex"]);  // last 0-indexed value
        Assert.Equal(3, result.Outputs["totalIterations"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithDelay_RespectsDelay()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 3,
            ["delayBetween"] = 50
        });

        var context = CreateContext();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await node.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        // 3 iterations with 50ms delay between = ~100ms (2 delays, not 3)
        Assert.True(sw.ElapsedMilliseconds >= 80, $"Expected >= 80ms, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteAsync_WithOnIteration_NoDelay()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 1,
            ["delayBetween"] = 0
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("done", result.OutputPort);
        Assert.Equal(0, result.Outputs["loopIndex"]);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOnCancellation()
    {
        var node = new LoopNode { Id = "loop1" };
        node.Configure(new Dictionary<string, object?>
        {
            ["count"] = 1000,
            ["delayBetween"] = 100
        });

        var cts = new CancellationTokenSource();
        var context = new FlowExecutionContext(new Flow { Name = "Cancel Test" }, cts.Token);

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => node.ExecuteAsync(context, cts.Token));
    }
}
