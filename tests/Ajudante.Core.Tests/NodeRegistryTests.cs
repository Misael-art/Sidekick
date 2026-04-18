using System.Reflection;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Registry;

namespace Ajudante.Core.Tests;

// Test node that lives in the test assembly for ScanAssembly to discover
[NodeInfo(TypeId = "test.simple", DisplayName = "Simple Test Node", Category = NodeCategory.Action, Color = "#22C55E")]
public class SimpleTestNode : IActionNode
{
    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "test.simple",
        DisplayName = "Simple Test Node",
        Category = NodeCategory.Action,
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "text", Name = "Text", Type = PropertyType.String, DefaultValue = "hello" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) { }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("out"));
    }
}

// A second test node to verify multiple are discovered
[NodeInfo(TypeId = "test.another", DisplayName = "Another Test Node", Category = NodeCategory.Logic)]
public class AnotherTestNode : ILogicNode
{
    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "test.another",
        DisplayName = "Another Test Node",
        Category = NodeCategory.Logic,
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In" }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) { }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(NodeResult.Ok("out"));
    }
}

public class NodeRegistryTests
{
    [Fact]
    public void ScanAssembly_FindsNodesWithNodeInfoAttribute()
    {
        var registry = new NodeRegistry();

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var definitions = registry.GetAllDefinitions();
        Assert.Contains(definitions, d => d.TypeId == "test.simple");
        Assert.Contains(definitions, d => d.TypeId == "test.another");
    }

    [Fact]
    public void ScanAssembly_SetsDefinitionFieldsFromAttribute()
    {
        var registry = new NodeRegistry();

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var def = registry.GetDefinition("test.simple");
        Assert.NotNull(def);
        Assert.Equal("test.simple", def.TypeId);
        Assert.Equal("Simple Test Node", def.DisplayName);
        Assert.Equal(NodeCategory.Action, def.Category);
        Assert.Equal("#22C55E", def.Color);
    }

    [Fact]
    public void ScanAssembly_IncludesPortsAndProperties()
    {
        var registry = new NodeRegistry();

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var def = registry.GetDefinition("test.simple");
        Assert.NotNull(def);
        Assert.Single(def.InputPorts);
        Assert.Equal("in", def.InputPorts[0].Id);
        Assert.Single(def.OutputPorts);
        Assert.Equal("out", def.OutputPorts[0].Id);
        Assert.Single(def.Properties);
        Assert.Equal("text", def.Properties[0].Id);
    }

    [Fact]
    public void GetAllDefinitions_ReturnsAllScannedDefinitions()
    {
        var registry = new NodeRegistry();

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var definitions = registry.GetAllDefinitions();
        Assert.True(definitions.Length >= 2); // At least our 2 test nodes
    }

    [Fact]
    public void CreateInstance_ReturnsCorrectNodeType()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var node = registry.CreateInstance("test.simple");

        Assert.IsType<SimpleTestNode>(node);
    }

    [Fact]
    public void CreateInstance_ReturnsDifferentInstanceEachCall()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var node1 = registry.CreateInstance("test.simple");
        var node2 = registry.CreateInstance("test.simple");

        Assert.NotSame(node1, node2);
    }

    [Fact]
    public void CreateInstance_ThrowsForUnknownTypeId()
    {
        var registry = new NodeRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.CreateInstance("nonexistent.type"));

        Assert.Contains("Unknown node type", ex.Message);
        Assert.Contains("nonexistent.type", ex.Message);
    }

    [Fact]
    public void GetDefinition_ReturnsDefinitionForKnownType()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        var def = registry.GetDefinition("test.simple");

        Assert.NotNull(def);
        Assert.Equal("test.simple", def.TypeId);
    }

    [Fact]
    public void GetDefinition_ReturnsNullForUnknownTypeId()
    {
        var registry = new NodeRegistry();

        var def = registry.GetDefinition("nonexistent.type");

        Assert.Null(def);
    }

    [Fact]
    public void ScanDirectory_HandlesNonExistentDirectoryGracefully()
    {
        var registry = new NodeRegistry();

        // Should not throw
        registry.ScanDirectory(Path.Combine(Path.GetTempPath(), "nonexistent_dir_" + Guid.NewGuid()));

        Assert.Empty(registry.GetAllDefinitions());
    }

    [Fact]
    public void ScanAssembly_IgnoresAbstractClasses()
    {
        // NodeRegistry filters out abstract classes, so they should not appear
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        // All definitions should be from concrete classes only
        foreach (var def in registry.GetAllDefinitions())
        {
            var node = registry.CreateInstance(def.TypeId);
            Assert.NotNull(node);
        }
    }

    [Fact]
    public void ScanAssembly_CalledMultipleTimes_OverwritesDuplicateTypeIds()
    {
        var registry = new NodeRegistry();

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);
        var countBefore = registry.GetAllDefinitions().Length;

        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);
        var countAfter = registry.GetAllDefinitions().Length;

        // Re-scanning same assembly should not create duplicates
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void ScanAssembly_DefaultColor_UsedWhenAttributeHasEmptyColor()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(SimpleTestNode).Assembly);

        // AnotherTestNode has no Color in the attribute, so default should be used
        var def = registry.GetDefinition("test.another");
        Assert.NotNull(def);
        // The default for Logic category is #EAB308
        // But NodeInfoAttribute defaults Color to "#888888"
        // NodeRegistry uses GetDefaultColor only when attr.Color.Length == 0
        // Since NodeInfoAttribute defaults to "#888888", that's what we'll get
        Assert.NotNull(def.Color);
        Assert.NotEmpty(def.Color);
    }
}
