using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class DelayNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new DelayNode();
        Assert.Equal("logic.delay", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasCorrectCategory()
    {
        var node = new DelayNode();
        Assert.Equal(NodeCategory.Logic, node.Definition.Category);
    }

    [Fact]
    public void Definition_HasInputPort()
    {
        var node = new DelayNode();
        Assert.Single(node.Definition.InputPorts);
        Assert.Equal("in", node.Definition.InputPorts[0].Id);
    }

    [Fact]
    public void Definition_HasOutputPort()
    {
        var node = new DelayNode();
        Assert.Single(node.Definition.OutputPorts);
        Assert.Equal("out", node.Definition.OutputPorts[0].Id);
    }

    [Fact]
    public void Definition_HasMillisecondsProperty()
    {
        var node = new DelayNode();
        Assert.Single(node.Definition.Properties);
        Assert.Equal("milliseconds", node.Definition.Properties[0].Id);
        Assert.Equal(PropertyType.Integer, node.Definition.Properties[0].Type);
    }

    [Fact]
    public void Configure_SetsMilliseconds()
    {
        var node = new DelayNode();
        node.Configure(new Dictionary<string, object?> { ["milliseconds"] = 500 });

        // We can verify Configure didn't throw — the effects are visible on ExecuteAsync
        Assert.NotNull(node);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOkWithOutPort()
    {
        var node = new DelayNode();
        node.Configure(new Dictionary<string, object?> { ["milliseconds"] = 10 });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("out", result.OutputPort);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsDelay()
    {
        var node = new DelayNode();
        node.Configure(new Dictionary<string, object?> { ["milliseconds"] = 100 });

        var context = CreateContext();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await node.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 80, $"Expected delay >= 80ms, was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOnCancellation()
    {
        var node = new DelayNode();
        node.Configure(new Dictionary<string, object?> { ["milliseconds"] = 5000 });

        var cts = new CancellationTokenSource();
        var context = new FlowExecutionContext(new Flow { Name = "Cancel Test" }, cts.Token);

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => node.ExecuteAsync(context, cts.Token));
    }
}
