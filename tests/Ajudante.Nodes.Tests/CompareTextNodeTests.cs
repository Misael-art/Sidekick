using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class CompareTextNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new CompareTextNode();
        Assert.Equal("logic.compareText", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasMatchAndNoMatchOutputPorts()
    {
        var node = new CompareTextNode();
        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "match");
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "noMatch");
    }

    [Fact]
    public void Definition_HasThreeProperties()
    {
        var node = new CompareTextNode();
        Assert.Equal(3, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "text1");
        Assert.Contains(node.Definition.Properties, p => p.Id == "text2");
        Assert.Contains(node.Definition.Properties, p => p.Id == "comparison");
    }

    [Fact]
    public async Task Equals_ReturnsMatch_WhenTextsEqual()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "hello",
            ["text2"] = "hello",
            ["comparison"] = "Equals"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }

    [Fact]
    public async Task Equals_IsCaseInsensitive()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "Hello",
            ["text2"] = "HELLO",
            ["comparison"] = "Equals"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }

    [Fact]
    public async Task Equals_ReturnsNoMatch_WhenDifferent()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "hello",
            ["text2"] = "world",
            ["comparison"] = "Equals"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("noMatch", result.OutputPort);
    }

    [Fact]
    public async Task Contains_ReturnsMatch_WhenContained()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "Hello World",
            ["text2"] = "world",
            ["comparison"] = "Contains"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }

    [Fact]
    public async Task StartsWith_ReturnsMatch()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "Hello World",
            ["text2"] = "hello",
            ["comparison"] = "StartsWith"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }

    [Fact]
    public async Task EndsWith_ReturnsMatch()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "Hello World",
            ["text2"] = "world",
            ["comparison"] = "EndsWith"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }

    [Fact]
    public async Task EndsWith_ReturnsNoMatch()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "Hello World",
            ["text2"] = "hello",
            ["comparison"] = "EndsWith"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("noMatch", result.OutputPort);
    }

    [Fact]
    public async Task UnknownComparison_ReturnsNoMatch()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "hello",
            ["text2"] = "hello",
            ["comparison"] = "Unknown"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("noMatch", result.OutputPort);
    }

    [Fact]
    public async Task ResolvesTemplateVariables()
    {
        var node = new CompareTextNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["text1"] = "{{greeting}}",
            ["text2"] = "hello",
            ["comparison"] = "Equals"
        });

        var context = CreateContext();
        context.SetVariable("greeting", "hello");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("match", result.OutputPort);
    }
}
