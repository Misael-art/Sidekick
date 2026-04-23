using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class TextTemplateNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Template Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public void Definition_HasExpectedShape()
    {
        var node = new TextTemplateNode();

        Assert.Equal("logic.textTemplate", node.Definition.TypeId);
        Assert.Single(node.Definition.InputPorts);
        Assert.Equal(2, node.Definition.OutputPorts.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "template");
        Assert.Contains(node.Definition.Properties, p => p.Id == "storeInVariable");
    }

    [Fact]
    public async Task ExecuteAsync_RendersVariableTemplates()
    {
        var node = new TextTemplateNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["template"] = "Hello {{name}}"
        });

        var context = CreateContext();
        context.SetVariable("name", "World");

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("out", result.OutputPort);
        Assert.Equal("Hello World", result.Outputs["text"]);
    }

    [Fact]
    public async Task ExecuteAsync_CanStoreRenderedTextInVariable()
    {
        var node = new TextTemplateNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["template"] = "Ticket {{number}}",
            ["storeInVariable"] = "summary"
        });

        var context = CreateContext();
        context.SetVariable("number", 42);

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Ticket 42", context.GetVariable("summary"));
    }

    [Fact]
    public async Task ExecuteAsync_CanReadNodeOutputReferences()
    {
        var node = new TextTemplateNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["template"] = "Result: {{previous.text}}"
        });

        var context = CreateContext();
        context.SetNodeOutputs("previous", new Dictionary<string, object?> { ["text"] = "Approved" });

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Result: Approved", result.Outputs["text"]);
    }
}
