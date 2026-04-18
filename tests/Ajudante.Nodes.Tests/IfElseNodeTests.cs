using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class IfElseNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Test Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new IfElseNode();
        Assert.Equal("logic.ifElse", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasTrueFalseOutputPorts()
    {
        var node = new IfElseNode();
        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "true");
        Assert.Contains(node.Definition.OutputPorts, p => p.Id == "false");
    }

    [Fact]
    public void Definition_HasThreeProperties()
    {
        var node = new IfElseNode();
        Assert.Equal(3, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "conditionType");
        Assert.Contains(node.Definition.Properties, p => p.Id == "variableName");
        Assert.Contains(node.Definition.Properties, p => p.Id == "compareValue");
    }

    [Fact]
    public async Task VariableEquals_ReturnsTrue_WhenMatch()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableEquals",
            ["variableName"] = "status",
            ["compareValue"] = "active"
        });

        var context = CreateContext();
        context.SetVariable("status", "active");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("true", result.OutputPort);
    }

    [Fact]
    public async Task VariableEquals_ReturnsFalse_WhenNoMatch()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableEquals",
            ["variableName"] = "status",
            ["compareValue"] = "active"
        });

        var context = CreateContext();
        context.SetVariable("status", "inactive");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("false", result.OutputPort);
    }

    [Fact]
    public async Task VariableEquals_IsCaseInsensitive()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableEquals",
            ["variableName"] = "name",
            ["compareValue"] = "HELLO"
        });

        var context = CreateContext();
        context.SetVariable("name", "hello");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("true", result.OutputPort);
    }

    [Fact]
    public async Task VariableContains_ReturnsTrue_WhenContained()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableContains",
            ["variableName"] = "sentence",
            ["compareValue"] = "world"
        });

        var context = CreateContext();
        context.SetVariable("sentence", "hello world!");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("true", result.OutputPort);
    }

    [Fact]
    public async Task VariableContains_ReturnsFalse_WhenNotContained()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableContains",
            ["variableName"] = "sentence",
            ["compareValue"] = "xyz"
        });

        var context = CreateContext();
        context.SetVariable("sentence", "hello world!");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("false", result.OutputPort);
    }

    [Fact]
    public async Task VariableGreaterThan_Numeric_ReturnsTrue()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableGreaterThan",
            ["variableName"] = "count",
            ["compareValue"] = "5"
        });

        var context = CreateContext();
        context.SetVariable("count", "10");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("true", result.OutputPort);
    }

    [Fact]
    public async Task VariableGreaterThan_Numeric_ReturnsFalse()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableGreaterThan",
            ["variableName"] = "count",
            ["compareValue"] = "100"
        });

        var context = CreateContext();
        context.SetVariable("count", "10");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("false", result.OutputPort);
    }

    [Fact]
    public async Task UnknownConditionType_ReturnsFalse()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "UnknownType",
            ["variableName"] = "x",
            ["compareValue"] = "y"
        });

        var context = CreateContext();
        context.SetVariable("x", "y");
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("false", result.OutputPort);
    }

    [Fact]
    public async Task VariableEquals_ReturnsFalse_WhenVariableMissing()
    {
        var node = new IfElseNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["conditionType"] = "VariableEquals",
            ["variableName"] = "nonexistent",
            ["compareValue"] = "anything"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        // Variable is null → empty string → doesn't match "anything"
        Assert.Equal("false", result.OutputPort);
    }
}
