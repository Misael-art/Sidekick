using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class SetVariableNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new SetVariableNode();
        Assert.Equal("logic.setVariable", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasCorrectPortLayout()
    {
        var node = new SetVariableNode();
        Assert.Single(node.Definition.InputPorts);
        Assert.Equal("in", node.Definition.InputPorts[0].Id);
        Assert.Single(node.Definition.OutputPorts);
        Assert.Equal("out", node.Definition.OutputPorts[0].Id);
    }

    [Fact]
    public void Definition_HasTwoProperties()
    {
        var node = new SetVariableNode();
        Assert.Equal(2, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "variableName");
        Assert.Contains(node.Definition.Properties, p => p.Id == "value");
    }

    [Fact]
    public async Task ExecuteAsync_SetsVariableInContext()
    {
        var node = new SetVariableNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["variableName"] = "myVar",
            ["value"] = "hello"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("out", result.OutputPort);
        Assert.Equal("hello", context.GetVariable("myVar"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOutputsWithVariableInfo()
    {
        var node = new SetVariableNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["variableName"] = "counter",
            ["value"] = "42"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("counter", result.Outputs["variableName"]);
        Assert.Equal("42", result.Outputs["value"]);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhenVariableNameEmpty()
    {
        var node = new SetVariableNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["variableName"] = "",
            ["value"] = "test"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Variable name is required", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesTemplateVariables()
    {
        var node = new SetVariableNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["variableName"] = "greeting",
            ["value"] = "Hello {{name}}"
        });

        var context = CreateContext();
        context.SetVariable("name", "World");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Hello World", context.GetVariable("greeting"));
    }
}
