using Ajudante.Core;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

/// <summary>
/// Tests for MouseClickNode covering definition and configuration only.
/// Actual mouse simulation (MoveTo, Click) requires a running Windows session
/// and is not suitable for unit tests.
/// </summary>
public class MouseClickNodeTests
{
    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new MouseClickNode();
        Assert.Equal("action.mouseClick", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasCorrectCategory()
    {
        var node = new MouseClickNode();
        Assert.Equal(NodeCategory.Action, node.Definition.Category);
    }

    [Fact]
    public void Definition_HasInputAndOutputPorts()
    {
        var node = new MouseClickNode();

        Assert.Single(node.Definition.InputPorts);
        Assert.Equal("in", node.Definition.InputPorts[0].Id);

        Assert.Single(node.Definition.OutputPorts);
        Assert.Equal("out", node.Definition.OutputPorts[0].Id);
    }

    [Fact]
    public void Definition_HasFourProperties()
    {
        var node = new MouseClickNode();
        Assert.Equal(4, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "x");
        Assert.Contains(node.Definition.Properties, p => p.Id == "y");
        Assert.Contains(node.Definition.Properties, p => p.Id == "button");
        Assert.Contains(node.Definition.Properties, p => p.Id == "clickType");
    }

    [Fact]
    public void Definition_ButtonProperty_HasCorrectOptions()
    {
        var node = new MouseClickNode();
        var buttonProp = node.Definition.Properties.Find(p => p.Id == "button")!;
        Assert.Equal(PropertyType.Dropdown, buttonProp.Type);
        Assert.Contains("Left", buttonProp.Options!);
        Assert.Contains("Right", buttonProp.Options!);
        Assert.Contains("Middle", buttonProp.Options!);
    }

    [Fact]
    public void Definition_ClickTypeProperty_HasCorrectOptions()
    {
        var node = new MouseClickNode();
        var clickProp = node.Definition.Properties.Find(p => p.Id == "clickType")!;
        Assert.Equal(PropertyType.Dropdown, clickProp.Type);
        Assert.Contains("Single", clickProp.Options!);
        Assert.Contains("Double", clickProp.Options!);
    }

    [Fact]
    public void Configure_DoesNotThrow()
    {
        var node = new MouseClickNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["x"] = 100,
            ["y"] = 200,
            ["button"] = "Right",
            ["clickType"] = "Double"
        });

        // No exception means configuration succeeded
        Assert.NotNull(node);
    }

    [Fact]
    public void Configure_WithDefaults_DoesNotThrow()
    {
        var node = new MouseClickNode();
        node.Configure(new Dictionary<string, object?>());
        Assert.NotNull(node);
    }

    [Fact]
    public void Id_CanBeSetAndRetrieved()
    {
        var node = new MouseClickNode { Id = "node123" };
        Assert.Equal("node123", node.Id);
    }
}
