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

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "action.writeFile",
            DisplayName = "Write File",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "" },
                new() { Id = "content", Name = "Content", Type = PropertyType.String, DefaultValue = "" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "action.browserClick",
            DisplayName = "Browser Click",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
                new() { Id = "automationId", Name = "Automation Id", Type = PropertyType.String, DefaultValue = "" },
                new() { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "" },
                new() { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = "" },
                new() { Id = "timeoutMs", Name = "Timeout", Type = PropertyType.Integer, DefaultValue = 5000 }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "trigger.imageDetected",
            DisplayName = "Image Trigger",
            Category = NodeCategory.Trigger,
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "triggered", Name = "Triggered" }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Id = "templateImage", Name = "Template Image", Type = PropertyType.ImageTemplate, DefaultValue = "" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "logic.textTemplate",
            DisplayName = "Text Template",
            Category = NodeCategory.Logic,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Id = "template", Name = "Template", Type = PropertyType.String, DefaultValue = "" },
                new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "" }
            }
        });

        registry.RegisterDefinition(new NodeDefinition
        {
            TypeId = "action.logMessage",
            DisplayName = "Log Message",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition>
            {
                new() { Id = "in", Name = "In" }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "out", Name = "Out" }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Id = "message", Name = "Message", Type = PropertyType.String, DefaultValue = "" }
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

    [Fact]
    public void Validate_MissingRequiredFilePath_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "action.writeFile", Properties = new Dictionary<string, object?> { ["content"] = "demo" } }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required property") && e.Contains("File Path"));
    }

    [Fact]
    public void Validate_InvalidConnectionPorts_ProducesErrors()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new() { Id = "n2", TypeId = "action.writeFile", Properties = new Dictionary<string, object?> { ["filePath"] = "C:\\temp\\demo.txt" } }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "invalid-out", TargetNodeId = "n2", TargetPort = "invalid-in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid source port"));
        Assert.Contains(result.Errors, e => e.Contains("invalid target port"));
    }

    [Fact]
    public void Validate_UnresolvedTemplateReference_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new()
                {
                    Id = "n2",
                    TypeId = "action.logMessage",
                    Properties = new Dictionary<string, object?> { ["message"] = "Hello {{missingValue}}" }
                }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unresolved reference 'missingValue'"));
    }

    [Fact]
    public void Validate_TemplateReferenceProducedByNode_IsAccepted()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "start", TypeId = "trigger.hotkey" },
                new()
                {
                    Id = "template",
                    TypeId = "logic.textTemplate",
                    Properties = new Dictionary<string, object?>
                    {
                        ["template"] = "demo",
                        ["storeInVariable"] = "generatedText"
                    }
                },
                new()
                {
                    Id = "log",
                    TypeId = "action.logMessage",
                    Properties = new Dictionary<string, object?> { ["message"] = "Value {{generatedText}}" }
                }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "start", SourcePort = "triggered", TargetNodeId = "template", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "template", SourcePort = "out", TargetNodeId = "log", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.Contains("generatedText"));
    }

    [Fact]
    public void Validate_IncompleteBrowserSelector_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new()
                {
                    Id = "n2",
                    TypeId = "action.browserClick",
                    Properties = new Dictionary<string, object?>
                    {
                        ["windowTitle"] = "",
                        ["automationId"] = "",
                        ["elementName"] = "",
                        ["controlType"] = "",
                        ["timeoutMs"] = 5000
                    }
                }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("incomplete selector"));
    }

    [Fact]
    public void Validate_InvalidImageTemplate_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.imageDetected", Properties = new Dictionary<string, object?> { ["templateImage"] = new Dictionary<string, object?>() } }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid image template"));
    }

    [Fact]
    public void Validate_InvalidTimeout_ProducesError()
    {
        var registry = CreateRegistryWithStandardTypes();
        var validator = new FlowValidator(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "trigger.hotkey" },
                new()
                {
                    Id = "n2",
                    TypeId = "action.browserClick",
                    Properties = new Dictionary<string, object?>
                    {
                        ["elementName"] = "Submit",
                        ["timeoutMs"] = 0
                    }
                }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "n1", SourcePort = "triggered", TargetNodeId = "n2", TargetPort = "in" }
            }
        };

        var result = validator.Validate(flow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid timeout"));
    }
}
