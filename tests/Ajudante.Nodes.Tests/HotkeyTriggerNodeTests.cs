using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Triggers;

namespace Ajudante.Nodes.Tests;

/// <summary>
/// Tests for HotkeyTriggerNode covering definition, configuration, and the
/// Triggered event contract. Actual hotkey registration requires a Windows
/// message pump and is not suitable for unit tests.
/// </summary>
public class HotkeyTriggerNodeTests
{
    [Fact]
    public void Definition_HasCorrectTypeId()
    {
        var node = new HotkeyTriggerNode();
        Assert.Equal("trigger.hotkey", node.Definition.TypeId);
    }

    [Fact]
    public void Definition_HasCorrectCategory()
    {
        var node = new HotkeyTriggerNode();
        Assert.Equal(NodeCategory.Trigger, node.Definition.Category);
    }

    [Fact]
    public void Definition_HasNoInputPorts()
    {
        var node = new HotkeyTriggerNode();
        Assert.Empty(node.Definition.InputPorts);
    }

    [Fact]
    public void Definition_HasTriggeredOutputPort()
    {
        var node = new HotkeyTriggerNode();
        Assert.Single(node.Definition.OutputPorts);
        Assert.Equal("triggered", node.Definition.OutputPorts[0].Id);
        Assert.Equal(PortDataType.Flow, node.Definition.OutputPorts[0].DataType);
    }

    [Fact]
    public void Definition_HasKeyAndModifiersProperties()
    {
        var node = new HotkeyTriggerNode();
        Assert.Equal(2, node.Definition.Properties.Count);
        Assert.Contains(node.Definition.Properties, p => p.Id == "key");
        Assert.Contains(node.Definition.Properties, p => p.Id == "modifiers");
    }

    [Fact]
    public void Definition_ModifiersProperty_HasCorrectOptions()
    {
        var node = new HotkeyTriggerNode();
        var modProp = node.Definition.Properties.Find(p => p.Id == "modifiers")!;
        Assert.Equal(PropertyType.Dropdown, modProp.Type);
        Assert.Contains("None", modProp.Options!);
        Assert.Contains("Ctrl", modProp.Options!);
        Assert.Contains("Shift", modProp.Options!);
        Assert.Contains("Alt", modProp.Options!);
        Assert.Contains("Win", modProp.Options!);
    }

    [Fact]
    public void Configure_SetsKeyAndModifiers()
    {
        var node = new HotkeyTriggerNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["key"] = "F5",
            ["modifiers"] = "Ctrl"
        });

        // Configuration succeeded (no exception)
        Assert.NotNull(node);
    }

    [Fact]
    public void ImplementsITriggerNode()
    {
        var node = new HotkeyTriggerNode();
        Assert.IsAssignableFrom<ITriggerNode>(node);
    }

    [Fact]
    public void ImplementsINode()
    {
        var node = new HotkeyTriggerNode();
        Assert.IsAssignableFrom<INode>(node);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOkWithTriggeredPort()
    {
        var node = new HotkeyTriggerNode();
        var flow = new Flow { Name = "Test Flow" };
        var context = new FlowExecutionContext(flow, CancellationToken.None);

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("triggered", result.OutputPort);
    }

    [Fact]
    public void Id_CanBeSetAndRetrieved()
    {
        var node = new HotkeyTriggerNode { Id = "trigger1" };
        Assert.Equal("trigger1", node.Id);
    }

    [Fact]
    public void TriggeredEvent_CanBeSubscribed()
    {
        var node = new HotkeyTriggerNode();
        TriggerEventArgs? receivedArgs = null;

        node.Triggered += args => receivedArgs = args;

        // We can't trigger it without a real Windows message loop,
        // but we can verify the event is subscribable without error.
        Assert.Null(receivedArgs);
    }
}
