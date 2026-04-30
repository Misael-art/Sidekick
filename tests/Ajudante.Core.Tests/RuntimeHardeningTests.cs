using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using System.Reflection;

namespace Ajudante.Core.Tests;

public class RuntimeHardeningTests
{
    [Fact]
    public async Task Executor_RaisesRuntimePhase_FromNodeContext()
    {
        var executor = new FlowExecutor(new TestRegistry(new PhaseNode()));
        var phases = new List<(string NodeId, string Phase, string? Message)>();
        executor.PhaseChanged += (nodeId, phase, message, _) => phases.Add((nodeId, phase, message));

        await executor.ExecuteAsync(new Flow
        {
            Id = "phase-flow",
            Nodes = [new NodeInstance { Id = "n1", TypeId = "test.phase" }]
        });

        var phase = Assert.Single(phases);
        Assert.Equal("n1", phase.NodeId);
        Assert.Equal(RuntimePhases.WaitingForElement, phase.Phase);
        Assert.Equal("waiting", phase.Message);
    }

    [Fact]
    public async Task Executor_StopsRunawayCycle_WithClearError()
    {
        var executor = new FlowExecutor(new TestRegistry(new PassNode()))
        {
            MaxStepsPerRun = 5
        };
        string? error = null;
        executor.FlowError += (_, message) => error = message;

        await executor.ExecuteAsync(new Flow
        {
            Id = "cycle",
            Nodes =
            [
                new NodeInstance { Id = "a", TypeId = "test.pass" },
                new NodeInstance { Id = "b", TypeId = "test.pass" }
            ],
            Connections =
            [
                new Connection { Id = "ab", SourceNodeId = "a", SourcePort = "out", TargetNodeId = "b", TargetPort = "in" },
                new Connection { Id = "ba", SourceNodeId = "b", SourcePort = "out", TargetNodeId = "a", TargetPort = "in" }
            ]
        }, startNodeId: "a");

        Assert.NotNull(error);
        Assert.Contains("step budget", error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PhaseNode : IActionNode
    {
        public string Id { get; set; } = "";
        public NodeDefinition Definition { get; } = DefinitionFor("test.phase");
        public void Configure(Dictionary<string, object?> properties) { }

        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
        {
            context.EmitPhase(RuntimePhases.WaitingForElement, "waiting");
            return Task.FromResult(NodeResult.Ok("out"));
        }
    }

    private sealed class PassNode : IActionNode
    {
        public string Id { get; set; } = "";
        public NodeDefinition Definition { get; } = DefinitionFor("test.pass");
        public void Configure(Dictionary<string, object?> properties) { }
        public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct) => Task.FromResult(NodeResult.Ok("out"));
    }

    private sealed class TestRegistry(INode node) : INodeRegistry
    {
        public void ScanAssembly(Assembly assembly) { }
        public void ScanDirectory(string pluginPath) { }
        public NodeDefinition[] GetAllDefinitions() => [node.Definition];
        public NodeDefinition? GetDefinition(string typeId) => node.Definition.TypeId == typeId ? node.Definition : null;
        public INode CreateInstance(string typeId) => node;
    }

    private static NodeDefinition DefinitionFor(string typeId) => new()
    {
        TypeId = typeId,
        DisplayName = typeId,
        Category = NodeCategory.Action,
        InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
        OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }]
    };
}
