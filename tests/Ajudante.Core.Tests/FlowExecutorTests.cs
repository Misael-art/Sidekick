using System.Reflection;
using System.Text.Json;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Tests;

public class FlowExecutorTests
{
    #region Mock Helpers

    private class MockNode : IActionNode
    {
        public string Id { get; set; } = "";

        public NodeDefinition Definition { get; set; } = new()
        {
            TypeId = "test.mock",
            DisplayName = "Mock Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        public Func<FlowExecutionContext, CancellationToken, Task<NodeResult>>? ExecuteFunc { get; set; }

        public void Configure(Dictionary<string, object?> properties) { }

        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            return ExecuteFunc != null
                ? ExecuteFunc(context, ct)
                : Task.FromResult(NodeResult.Ok("out"));
        }
    }

    private class BranchingMockNode : ILogicNode
    {
        public string Id { get; set; } = "";

        public NodeDefinition Definition { get; set; } = new()
        {
            TypeId = "test.branch",
            DisplayName = "Branch Node",
            Category = NodeCategory.Logic,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "true", Name = "True" },
                new() { Id = "false", Name = "False" }
            }
        };

        public string OutputPortToUse { get; set; } = "true";

        public void Configure(Dictionary<string, object?> properties) { }

        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(NodeResult.Ok(OutputPortToUse));
        }
    }

    private class MockNodeRegistry : INodeRegistry
    {
        private readonly Dictionary<string, NodeDefinition> _definitions = new();
        private readonly Dictionary<string, Func<INode>> _factories = new();

        public void Register(string typeId, NodeDefinition definition, Func<INode> factory)
        {
            _definitions[typeId] = definition;
            _factories[typeId] = factory;
        }

        public void ScanAssembly(Assembly assembly) { }
        public void ScanDirectory(string pluginPath) { }
        public NodeDefinition[] GetAllDefinitions() => _definitions.Values.ToArray();

        public INode CreateInstance(string typeId)
        {
            if (!_factories.TryGetValue(typeId, out var factory))
                throw new InvalidOperationException($"Unknown node type: {typeId}");
            return factory();
        }

        public NodeDefinition? GetDefinition(string typeId) =>
            _definitions.TryGetValue(typeId, out var def) ? def : null;
    }

    private static MockNodeRegistry CreateDefaultRegistry()
    {
        var registry = new MockNodeRegistry();

        var actionDef = new NodeDefinition
        {
            TypeId = "test.action",
            DisplayName = "Test Action",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.action", actionDef, () => new MockNode { Definition = actionDef });

        return registry;
    }

    private static MockNodeRegistry CreateRegistryWithBranching()
    {
        var registry = CreateDefaultRegistry();

        var branchDef = new NodeDefinition
        {
            TypeId = "test.branch",
            DisplayName = "Branch",
            Category = NodeCategory.Logic,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition>
            {
                new() { Id = "true", Name = "True" },
                new() { Id = "false", Name = "False" }
            }
        };

        registry.Register("test.branch", branchDef,
            () => new BranchingMockNode { Definition = branchDef, OutputPortToUse = "true" });

        return registry;
    }

    #endregion

    [Fact]
    public async Task ExecuteAsync_LinearFlow_ExecutesAllNodesInOrder()
    {
        var executionOrder = new List<string>();
        var registry = new MockNodeRegistry();

        var actionDef = new NodeDefinition
        {
            TypeId = "test.action",
            DisplayName = "Test",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.action", actionDef, () =>
        {
            var node = new MockNode
            {
                Definition = actionDef,
                ExecuteFunc = (ctx, ct) =>
                {
                    // We capture the Id that was assigned after CreateInstance
                    return Task.FromResult(NodeResult.Ok("out"));
                }
            };
            return node;
        });

        var executor = new FlowExecutor(registry);

        // Track execution order via NodeStatusChanged events
        var runningOrder = new List<string>();
        executor.NodeStatusChanged += (nodeId, status) =>
        {
            if (status == NodeStatus.Running)
                runningOrder.Add(nodeId);
        };

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "A", TypeId = "test.action" },
                new() { Id = "B", TypeId = "test.action" },
                new() { Id = "C", TypeId = "test.action" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "A", SourcePort = "out", TargetNodeId = "B", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "B", SourcePort = "out", TargetNodeId = "C", TargetPort = "in" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal(new List<string> { "A", "B", "C" }, runningOrder);
    }

    [Fact]
    public async Task ExecuteAsync_BranchingFlow_FollowsCorrectOutputPort()
    {
        var registry = CreateRegistryWithBranching();
        var executor = new FlowExecutor(registry);

        var executedNodes = new List<string>();
        executor.NodeStatusChanged += (nodeId, status) =>
        {
            if (status == NodeStatus.Running)
                executedNodes.Add(nodeId);
        };

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "branch", TypeId = "test.branch" },
                new() { Id = "trueTarget", TypeId = "test.action" },
                new() { Id = "falseTarget", TypeId = "test.action" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "branch", SourcePort = "true", TargetNodeId = "trueTarget", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "branch", SourcePort = "false", TargetNodeId = "falseTarget", TargetPort = "in" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Contains("branch", executedNodes);
        Assert.Contains("trueTarget", executedNodes);
        Assert.DoesNotContain("falseTarget", executedNodes);
    }

    [Fact]
    public async Task ExecuteAsync_FiresNodeStatusChanged_RunningThenCompleted()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        var statusChanges = new List<(string NodeId, NodeStatus Status)>();
        executor.NodeStatusChanged += (nodeId, status) =>
            statusChanges.Add((nodeId, status));

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.action" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Contains(statusChanges, s => s.NodeId == "n1" && s.Status == NodeStatus.Running);
        Assert.Contains(statusChanges, s => s.NodeId == "n1" && s.Status == NodeStatus.Completed);

        // Running should come before Completed
        var runningIdx = statusChanges.FindIndex(s => s.NodeId == "n1" && s.Status == NodeStatus.Running);
        var completedIdx = statusChanges.FindIndex(s => s.NodeId == "n1" && s.Status == NodeStatus.Completed);
        Assert.True(runningIdx < completedIdx);
    }

    [Fact]
    public async Task ExecuteAsync_FiresFlowCompleted_OnSuccess()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        string? completedFlowId = null;
        executor.FlowCompleted += id => completedFlowId = id;

        var flow = new Flow
        {
            Id = "test-flow",
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.action" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal("test-flow", completedFlowId);
    }

    [Fact]
    public async Task ExecuteAsync_FiresFlowError_WhenNoEntryNodeFound()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        string? errorFlowId = null;
        string? errorMessage = null;
        executor.FlowError += (id, msg) =>
        {
            errorFlowId = id;
            errorMessage = msg;
        };

        // Empty flow has no entry node
        var flow = new Flow
        {
            Id = "empty-flow",
            Nodes = new List<NodeInstance>()
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal("empty-flow", errorFlowId);
        Assert.NotNull(errorMessage);
        Assert.Contains("entry node", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntryNodeIsMissing_FiresSingleFlowError_AndDoesNotComplete()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        var flowErrorCount = 0;
        var flowCompletedCount = 0;

        executor.FlowError += (_, _) => flowErrorCount++;
        executor.FlowCompleted += _ => flowCompletedCount++;

        var flow = new Flow
        {
            Id = "empty-flow",
            Nodes = new List<NodeInstance>()
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal(1, flowErrorCount);
        Assert.Equal(0, flowCompletedCount);
    }

    [Fact]
    public async Task ExecuteAsync_NodeError_FiresErrorStatus()
    {
        var registry = new MockNodeRegistry();

        var failDef = new NodeDefinition
        {
            TypeId = "test.fail",
            DisplayName = "Fail Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.fail", failDef, () => new MockNode
        {
            Definition = failDef,
            ExecuteFunc = (_, _) => Task.FromResult(NodeResult.Fail("Something went wrong"))
        });

        var executor = new FlowExecutor(registry);

        var statusChanges = new List<(string NodeId, NodeStatus Status)>();
        executor.NodeStatusChanged += (nodeId, status) =>
            statusChanges.Add((nodeId, status));

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.fail" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Contains(statusChanges, s => s.NodeId == "n1" && s.Status == NodeStatus.Error);
    }

    [Fact]
    public async Task Cancel_StopsExecution()
    {
        var registry = new MockNodeRegistry();

        var slowDef = new NodeDefinition
        {
            TypeId = "test.slow",
            DisplayName = "Slow Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.slow", slowDef, () => new MockNode
        {
            Definition = slowDef,
            ExecuteFunc = async (_, ct) =>
            {
                await Task.Delay(5000, ct);
                return NodeResult.Ok("out");
            }
        });

        var executor = new FlowExecutor(registry);

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.slow" }
            }
        };

        var executeTask = executor.ExecuteAsync(flow);

        // Give it a moment to start, then cancel
        await Task.Delay(100);
        executor.Cancel();

        await executeTask; // Should complete quickly after cancel

        Assert.False(executor.IsRunning);
    }

    [Fact]
    public async Task IsRunning_IsTrueDuringExecution_FalseAfter()
    {
        var registry = new MockNodeRegistry();
        bool wasRunningDuringExecution = false;

        var checkDef = new NodeDefinition
        {
            TypeId = "test.check",
            DisplayName = "Check Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        FlowExecutor? executorRef = null;
        registry.Register("test.check", checkDef, () => new MockNode
        {
            Definition = checkDef,
            ExecuteFunc = (_, _) =>
            {
                wasRunningDuringExecution = executorRef!.IsRunning;
                return Task.FromResult(NodeResult.Ok("out"));
            }
        });

        var executor = new FlowExecutor(registry);
        executorRef = executor;

        Assert.False(executor.IsRunning);

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.check" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.True(wasRunningDuringExecution);
        Assert.False(executor.IsRunning);
    }

    [Fact]
    public async Task ExecuteAsync_NodeException_SetsErrorStatus_ContinuesExecution()
    {
        var registry = new MockNodeRegistry();

        var throwDef = new NodeDefinition
        {
            TypeId = "test.throw",
            DisplayName = "Throw Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.throw", throwDef, () => new MockNode
        {
            Definition = throwDef,
            ExecuteFunc = (_, _) => throw new InvalidOperationException("Test exception")
        });

        var executor = new FlowExecutor(registry);

        var statusChanges = new List<(string, NodeStatus)>();
        executor.NodeStatusChanged += (id, status) => statusChanges.Add((id, status));

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "n1", TypeId = "test.throw" }
            }
        };

        // Should not throw
        await executor.ExecuteAsync(flow);

        Assert.Contains(statusChanges, s => s.Item1 == "n1" && s.Item2 == NodeStatus.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ConfiguresNodeProperties()
    {
        var registry = new MockNodeRegistry();
        Dictionary<string, object?>? configuredProps = null;

        var configDef = new NodeDefinition
        {
            TypeId = "test.config",
            DisplayName = "Config Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.config", configDef, () =>
        {
            var node = new ConfigurableMockNode
            {
                Definition = configDef,
                OnConfigure = props => configuredProps = new Dictionary<string, object?>(props)
            };
            return node;
        });

        var executor = new FlowExecutor(registry);

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "n1",
                    TypeId = "test.config",
                    Properties = new Dictionary<string, object?> { { "x", 100 }, { "y", 200 } }
                }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.NotNull(configuredProps);
        Assert.Equal(100, configuredProps["x"]);
        Assert.Equal(200, configuredProps["y"]);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesJsonElementPropertiesBeforeConfigure()
    {
        var registry = new MockNodeRegistry();
        Dictionary<string, object?>? configuredProps = null;

        var configDef = new NodeDefinition
        {
            TypeId = "test.jsonConfig",
            DisplayName = "Json Config Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.jsonConfig", configDef, () =>
        {
            var node = new ConfigurableMockNode
            {
                Definition = configDef,
                OnConfigure = props => configuredProps = new Dictionary<string, object?>(props)
            };
            return node;
        });

        using var document = JsonDocument.Parse("""{ "count": 7, "nested": { "name": "sidekick" } }""");

        var executor = new FlowExecutor(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "n1",
                    TypeId = "test.jsonConfig",
                    Properties = new Dictionary<string, object?>
                    {
                        ["count"] = document.RootElement.GetProperty("count").Clone(),
                        ["nested"] = document.RootElement.GetProperty("nested").Clone()
                    }
                }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.NotNull(configuredProps);
        Assert.Equal(7, configuredProps["count"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(configuredProps["nested"]);
        Assert.Equal("sidekick", nested["name"]);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesStructuredSnipAssetPayloadBeforeConfigure()
    {
        var registry = new MockNodeRegistry();
        Dictionary<string, object?>? configuredProps = null;

        var configDef = new NodeDefinition
        {
            TypeId = "test.snipConfig",
            DisplayName = "Snip Config Node",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.snipConfig", configDef, () =>
        {
            var node = new ConfigurableMockNode
            {
                Definition = configDef,
                OnConfigure = props => configuredProps = new Dictionary<string, object?>(props)
            };
            return node;
        });

        using var document = JsonDocument.Parse("""
        {
          "templateImage": {
            "kind": "snipAsset",
            "assetId": "asset-123",
            "displayName": "Header Button",
            "imagePath": "assets/snips/asset-123.png",
            "imageBase64": "AQIDBA=="
          },
          "threshold": 0.9
        }
        """);

        var executor = new FlowExecutor(registry);
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "n1",
                    TypeId = "test.snipConfig",
                    Properties = new Dictionary<string, object?>
                    {
                        ["templateImage"] = document.RootElement.GetProperty("templateImage").Clone(),
                        ["threshold"] = document.RootElement.GetProperty("threshold").Clone()
                    }
                }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.NotNull(configuredProps);
        Assert.Equal(0.9, configuredProps["threshold"]);

        var templateImage = Assert.IsType<Dictionary<string, object?>>(configuredProps["templateImage"]);
        Assert.Equal("snipAsset", templateImage["kind"]);
        Assert.Equal("asset-123", templateImage["assetId"]);
        Assert.Equal("Header Button", templateImage["displayName"]);
        Assert.Equal("assets/snips/asset-123.png", templateImage["imagePath"]);
        Assert.Equal("AQIDBA==", templateImage["imageBase64"]);
    }

    [Fact]
    public async Task ExecuteAsync_FindsEntryNode_WithNoIncomingConnections()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        var executedNodes = new List<string>();
        executor.NodeStatusChanged += (nodeId, status) =>
        {
            if (status == NodeStatus.Running)
                executedNodes.Add(nodeId);
        };

        // B is connected from A, C is connected from B
        // A has no incoming connections, so it's the entry node
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "B", TypeId = "test.action" },
                new() { Id = "A", TypeId = "test.action" },
                new() { Id = "C", TypeId = "test.action" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "A", SourcePort = "out", TargetNodeId = "B", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "B", SourcePort = "out", TargetNodeId = "C", TargetPort = "in" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal("A", executedNodes.First());
    }

    [Fact]
    public async Task ExecuteAsync_WithStartNodeId_StartsFromSpecifiedNode()
    {
        var registry = CreateDefaultRegistry();
        var executor = new FlowExecutor(registry);

        var executedNodes = new List<string>();
        executor.NodeStatusChanged += (nodeId, status) =>
        {
            if (status == NodeStatus.Running)
                executedNodes.Add(nodeId);
        };

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "A", TypeId = "test.action" },
                new() { Id = "B", TypeId = "test.action" },
                new() { Id = "C", TypeId = "test.action" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "A", SourcePort = "out", TargetNodeId = "B", TargetPort = "in" },
                new() { Id = "c2", SourceNodeId = "B", SourcePort = "out", TargetNodeId = "C", TargetPort = "in" }
            }
        };

        await executor.ExecuteAsync(flow, startNodeId: "B");

        Assert.DoesNotContain("A", executedNodes);
        Assert.Contains("B", executedNodes);
        Assert.Contains("C", executedNodes);
    }

    [Fact]
    public async Task ExecuteAsync_SetsNodeOutputsInContext()
    {
        var registry = new MockNodeRegistry();
        object? capturedOutput = null;

        var producerDef = new NodeDefinition
        {
            TypeId = "test.producer",
            DisplayName = "Producer",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        var consumerDef = new NodeDefinition
        {
            TypeId = "test.consumer",
            DisplayName = "Consumer",
            Category = NodeCategory.Action,
            InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In" } },
            OutputPorts = new List<PortDefinition> { new() { Id = "out", Name = "Out" } }
        };

        registry.Register("test.producer", producerDef, () => new MockNode
        {
            Definition = producerDef,
            ExecuteFunc = (_, _) => Task.FromResult(NodeResult.Ok("out",
                new Dictionary<string, object?> { { "value", 42 } }))
        });

        registry.Register("test.consumer", consumerDef, () => new MockNode
        {
            Definition = consumerDef,
            ExecuteFunc = (ctx, _) =>
            {
                capturedOutput = ctx.GetNodeOutput("producer", "value");
                return Task.FromResult(NodeResult.Ok("out"));
            }
        });

        var executor = new FlowExecutor(registry);

        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new() { Id = "producer", TypeId = "test.producer" },
                new() { Id = "consumer", TypeId = "test.consumer" }
            },
            Connections = new List<Connection>
            {
                new() { Id = "c1", SourceNodeId = "producer", SourcePort = "out", TargetNodeId = "consumer", TargetPort = "in" }
            }
        };

        await executor.ExecuteAsync(flow);

        Assert.Equal(42, capturedOutput);
    }

    #region Helper Node Types

    private class ConfigurableMockNode : IActionNode
    {
        public string Id { get; set; } = "";
        public NodeDefinition Definition { get; set; } = new()
        {
            TypeId = "test.config",
            DisplayName = "Config",
            Category = NodeCategory.Action
        };

        public Action<Dictionary<string, object?>>? OnConfigure { get; set; }

        public void Configure(Dictionary<string, object?> properties)
        {
            OnConfigure?.Invoke(properties);
        }

        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(NodeResult.Ok("out"));
        }
    }

    #endregion
}
