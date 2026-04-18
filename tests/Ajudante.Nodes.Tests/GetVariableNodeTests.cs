using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class GetVariableNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new GetVariableNode();
        Assert.Equal("logic.getVariable", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasCorrectPorts()
    {
        var node = new GetVariableNode();

        Assert.Single(node.Definition.InputPorts);
        Assert.Equal("in", node.Definition.InputPorts[0].Id);

        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "out");
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "value");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsVariableValue()
    {
        var node = new GetVariableNode();
        node.Configure(new Dictionary<string, object?> { ["variableName"] = "myVar" });

        var context = CreateContext();
        context.SetVariable("myVar", "hello world");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("out", result.OutputPort);
        Assert.Equal("hello world", result.Outputs["value"]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNullForMissingVariable()
    {
        var node = new GetVariableNode();
        node.Configure(new Dictionary<string, object?> { ["variableName"] = "nonexistent" });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Outputs["value"]);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhenVariableNameEmpty()
    {
        var node = new GetVariableNode();
        node.Configure(new Dictionary<string, object?> { ["variableName"] = "" });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Variable name is required", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesTemplateInVariableName()
    {
        var node = new GetVariableNode();
        node.Configure(new Dictionary<string, object?> { ["variableName"] = "{{target}}" });

        var context = CreateContext();
        context.SetVariable("target", "actual_var");
        context.SetVariable("actual_var", "resolved_value");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("resolved_value", result.Outputs["value"]);
    }
}
