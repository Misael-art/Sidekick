using System.Reflection;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Tests;

public class FlowValidatorTests
{
    private class MockNodeRegistry : INodeRegistry
    {
        private readonly Dictionary<string, NodeDefinition> _definitions = new();

        public void RegisterDefinition(NodeDefinition definition)
        {
            _definitions[definition.TypeId] = definition;
        }

        public void ScanAssembly(Assembly assembly) { }
        public void ScanDirectory(string pluginPath) { }

        public NodeDefinition[] GetAllDefinitions() => _definitions.Values.ToArray();

        public INode CreateInstance(string typeId) =>
            throw new NotImplementedException("Not needed for validator tests");

        public NodeDefinition? GetDefinition(string typeId) =>
            _definitions.TryGetValue(typeId, out var def) ? def : null;
    }

    private static MockNodeRegistry CreateRegistryWithStandardTypes()
    {
        var registry = new MockNodeRegistry();

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "trigger.hotkey",
            DisplayName = "Hotkey Trigger",
            Category = NodeCategory.Trigger,
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "triggered", Name = "Triggered" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "action.mouseClick",
            DisplayName = "Mouse Click",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "logic.delay",
            DisplayName = "Delay",
            Category = NodeCategory.Logic,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "logic.ifElse",
            DisplayName = "If/Else",
            Category = NodeCategory.Logic,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "true", Name = "True" },
                new() { Id = "false", Name = "False" }
            }
        });

        return registry;
    }

    [Fact]
    public void Validate_EmptyFlow_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow { Nodes = new List<NodeInstance>() };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no nodes"));
    }

    [Fact]
    public void Validate_UnknownNodeType_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "unknown.type" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown type") && e.Contains("unknown.type"));
    }

    [Fact]
    public void Validate_ConnectionReferencingMissingSourceNode_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "missing-node",
                    SourcePort = "out",
                    TargetNodeId = "n1",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing source node") && e.Contains("missing-node"));
    }

    [Fact]
    public void Validate_ConnectionReferencingMissingTargetNode_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "n1",
                    SourcePort = "out",
                    TargetNodeId = "missing-target",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing") && e.Contains("missing-target"));
    }

    [Fact]
    public void Validate_FlowWithoutTrigger_ProducesWarning()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "action.mouseClick" },
                new() { Id = "n2", TypeId = "logic.delay" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "n1",
                    SourcePort = "out",
                    TargetNodeId = "n2",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.True(result.IsValid); // Warnings don't make it invalid
        Assert.Contains(result.Warnings, w => w.Contains("no trigger"));
    }

    [Fact]
    public void Validate_DisconnectedNodes_ProducesWarning()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "action.mouseClick" }, // connected
                new() { Id = "n3", TypeId = "logic.delay" } // disconnected
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "n1",
                    SourcePort = "triggered",
                    TargetNodeId = "n2",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.Contains(result.Warnings, w => w.Contains("n3") && w.Contains("disconnected"));
    }

    [Fact]
    public void Validate_FlowWithCycle_ProducesWarning()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "logic.delay" },
                new() { Id = "n3", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "n2", SourcePort = "out", TargetNodeId = "n3", TargetPort = "in" },
                new() { Id = "c3", SourceNodeId = "n3", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" } // cycle: n3 -> n2
            }
        };

        var result = validator.Validate(flow);

        Assert.Contains(result.Warnings, w => w.Contains("cycle"));
    }

    [Fact]
    public void Validate_ValidFlow_ReturnsIsValidTrue()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "n1",
                    SourcePort = "triggered",
                    TargetNodeId = "n2",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ValidFlowWithTrigger_HasNoTriggerWarning()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "n1",
                    SourcePort = "triggered",
                    TargetNodeId = "n2",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("no trigger"));
    }

    [Fact]
    public void Validate_MultipleErrors_CollectsAll()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "unknown.type1" },
                new() { Id = "n2", TypeId = "unknown.type2" }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "c1",
                    SourceNodeId = "missing-src",
                    SourcePort = "out",
                    TargetNodeId = "missing-tgt",
                    TargetPort = "in"
                }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        // Should have errors for both unknown types and both missing connection nodes
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Validate_SingleNode_NoDisconnectedWarning()
    {
        // A single disconnected node should NOT trigger the disconnected warning
        // because the code checks flow.Nodes.Count > 1
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" }
            }
        };

        var result = validator.Validate(flow);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("disconnected"));
    }

    [Fact]
    public void Validate_NoCycle_NoCycleWarning()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "logic.delay" },
                new() { Id = "n3", TypeId = "action.mouseClick" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "n2", SourcePort = "out", TargetNodeId = "n3", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("cycle"));
    }
}
