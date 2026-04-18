using Ajudante.Core;
using Ajudante.Core.Engine;

namespace Ajudante.Core.Tests;

public class FlowExecutionContextTests
{
    private static Flow CreateFlowWithVariables(params FlowVariable[] variables)
    {
        return new Flow
        {
            Name = "Test Flow",
            Variables = variables.ToList()
        };
    }

    [Fact]
    public void Constructor_InitializesVariablesFromFlowDefaults()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "name", Type = VariableType.String, Default = "hello" },
            new FlowVariable { Name = "count", Type = VariableType.Integer, Default = 42 }
        );

        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Equal("hello", context.GetVariable("name"));
        Assert.Equal(42, context.GetVariable("count"));
    }

    [Fact]
    public void Constructor_InitializesNullDefaultsCorrectly()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "empty", Type = VariableType.String, Default = null }
        );

        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Null(context.GetVariable("empty"));
    }

    [Fact]
    public void Constructor_WithNoVariables_CreatesEmptyContext()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Null(context.GetVariable("anything"));
    }

    [Fact]
    public void SetVariable_GetVariable_RoundTrips()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("key", "value");

        Assert.Equal("value", context.GetVariable("key"));
    }

    [Fact]
    public void SetVariable_OverwritesExistingValue()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "x", Type = VariableType.String, Default = "old" }
        );
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("x", "new");

        Assert.Equal("new", context.GetVariable("x"));
    }

    [Fact]
    public void SetVariable_AcceptsNullValue()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("key", null);

        Assert.Null(context.GetVariable("key"));
    }

    [Fact]
    public void GetVariable_ReturnsNullForUnknownName()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Null(context.GetVariable("nonexistent"));
    }

    [Fact]
    public void GetVariableT_WithMatchingType_ReturnsTypedValue()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("count", 42);

        Assert.Equal(42, context.GetVariable<int>("count"));
    }

    [Fact]
    public void GetVariableT_WithTypeConversion_StringToInt()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("num", "123");

        Assert.Equal(123, context.GetVariable<int>("num"));
    }

    [Fact]
    public void GetVariableT_WithTypeConversion_IntToString()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("num", 456);

        Assert.Equal("456", context.GetVariable<string>("num"));
    }

    [Fact]
    public void GetVariableT_WithInconvertibleType_ReturnsDefault()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("text", "not-a-number");

        Assert.Equal(0, context.GetVariable<int>("text"));
    }

    [Fact]
    public void GetVariableT_WithNullValue_ReturnsDefault()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetVariable("key", null);

        Assert.Equal(0, context.GetVariable<int>("key"));
        Assert.Null(context.GetVariable<string>("key"));
    }

    [Fact]
    public void GetVariableT_ForUnknownName_ReturnsDefault()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Equal(0, context.GetVariable<int>("unknown"));
        Assert.Null(context.GetVariable<string>("unknown"));
        Assert.False(context.GetVariable<bool>("unknown"));
    }

    [Fact]
    public void SetNodeOutputs_GetNodeOutput_RoundTrips()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        var outputs = new Dictionary<string, object?>
        {
            { "x", 100 },
            { "y", 200 },
            { "label", "found" }
        };

        context.SetNodeOutputs("node-1", outputs);

        Assert.Equal(100, context.GetNodeOutput("node-1", "x"));
        Assert.Equal(200, context.GetNodeOutput("node-1", "y"));
        Assert.Equal("found", context.GetNodeOutput("node-1", "label"));
    }

    [Fact]
    public void GetNodeOutput_ReturnsNullForUnknownNode()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Null(context.GetNodeOutput("unknown-node", "port"));
    }

    [Fact]
    public void GetNodeOutput_ReturnsNullForUnknownPort()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetNodeOutputs("node-1", new Dictionary<string, object?> { { "x", 100 } });

        Assert.Null(context.GetNodeOutput("node-1", "unknown-port"));
    }

    [Fact]
    public void SetNodeOutputs_OverwritesPreviousOutputs()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        context.SetNodeOutputs("node-1", new Dictionary<string, object?> { { "x", 1 } });
        context.SetNodeOutputs("node-1", new Dictionary<string, object?> { { "x", 2 } });

        Assert.Equal(2, context.GetNodeOutput("node-1", "x"));
    }

    [Fact]
    public void ResolveTemplate_ReplacesVariableWithValue()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "path", Type = VariableType.String, Default = "C:\\Downloads" }
        );
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        var result = context.ResolveTemplate("Save to {{path}}");

        Assert.Equal("Save to C:\\Downloads", result);
    }

    [Fact]
    public void ResolveTemplate_ReplacesMultipleVariables()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "first", Type = VariableType.String, Default = "Hello" },
            new FlowVariable { Name = "second", Type = VariableType.String, Default = "World" }
        );
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        var result = context.ResolveTemplate("{{first}} {{second}}!");

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void ResolveTemplate_LeavesUnknownVariablesUnchanged()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        var result = context.ResolveTemplate("Value is {{unknown}}");

        Assert.Equal("Value is {{unknown}}", result);
    }

    [Fact]
    public void ResolveTemplate_HandlesEmptyString()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Equal("", context.ResolveTemplate(""));
    }

    [Fact]
    public void ResolveTemplate_HandlesNullString()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Null(context.ResolveTemplate(null!));
    }

    [Fact]
    public void ResolveTemplate_HandlesStringWithoutTemplates()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Equal("plain text", context.ResolveTemplate("plain text"));
    }

    [Fact]
    public void ResolveTemplate_UsesCurrentVariableValue()
    {
        var flow = CreateFlowWithVariables(
            new FlowVariable { Name = "x", Type = VariableType.String, Default = "initial" }
        );
        var context = new FlowExecutionContext(flow, CancellationToken.None);
        context.SetVariable("x", "updated");

        var result = context.ResolveTemplate("{{x}}");

        Assert.Equal("updated", result);
    }

    [Fact]
    public void Flow_Property_ReturnsSameFlowPassedToConstructor()
    {
        var flow = CreateFlowWithVariables();
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        Assert.Same(flow, context.Flow);
    }

    [Fact]
    public void CancellationToken_Property_ReturnsSameTokenPassedToConstructor()
    {
        var flow = CreateFlowWithVariables();
        var cts = new CancellationTokenSource();
        var context = new FlowExecutionContext(flow, cts.Token);

        Assert.Equal(cts.Token, context.CancellationToken);
    }
}
