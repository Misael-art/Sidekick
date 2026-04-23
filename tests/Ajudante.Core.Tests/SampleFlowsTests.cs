using Ajudante.Core.Serialization;

namespace Ajudante.Core.Tests;

public class SampleFlowsTests
{
    [Fact]
    public void SampleFlows_AreValidAndHaveUniqueIds()
    {
        var sampleFlowsDirectory = GetSampleFlowsDirectory();

        Assert.True(Directory.Exists(sampleFlowsDirectory), $"Sample flows directory not found: {sampleFlowsDirectory}");

        var sampleFlowPaths = Directory.GetFiles(sampleFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(sampleFlowPaths);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sampleFlowPath in sampleFlowPaths)
        {
            var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
            Assert.NotNull(flow);

            Assert.False(string.IsNullOrWhiteSpace(flow.Id), $"Sample flow '{sampleFlowPath}' is missing an id.");
            Assert.True(seenIds.Add(flow.Id), $"Duplicate sample flow id '{flow.Id}' found.");
            Assert.False(string.IsNullOrWhiteSpace(flow.Name), $"Sample flow '{sampleFlowPath}' is missing a name.");
            Assert.NotEmpty(flow.Nodes);
            Assert.Contains(flow.Nodes, node => string.Equals(node.TypeId, "trigger.manualStart", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SampleFlows_HaveValidNodeAndConnectionReferences()
    {
        foreach (var sampleFlowPath in GetSampleFlowPaths())
        {
            var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
            Assert.NotNull(flow);

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in flow.Nodes)
            {
                Assert.False(string.IsNullOrWhiteSpace(node.Id), $"Sample flow '{sampleFlowPath}' has a node without id.");
                Assert.False(string.IsNullOrWhiteSpace(node.TypeId), $"Sample flow '{sampleFlowPath}' has a node without typeId.");
                Assert.True(nodeIds.Add(node.Id), $"Sample flow '{sampleFlowPath}' has duplicate node id '{node.Id}'.");
            }

            foreach (var connection in flow.Connections)
            {
                Assert.False(string.IsNullOrWhiteSpace(connection.Id), $"Sample flow '{sampleFlowPath}' has a connection without id.");
                Assert.Contains(connection.SourceNodeId, nodeIds);
                Assert.Contains(connection.TargetNodeId, nodeIds);
                Assert.False(string.IsNullOrWhiteSpace(connection.SourcePort), $"Sample flow '{sampleFlowPath}' has a connection without source port.");
                Assert.False(string.IsNullOrWhiteSpace(connection.TargetPort), $"Sample flow '{sampleFlowPath}' has a connection without target port.");
            }
        }
    }

    [Fact]
    public void SampleFlows_RoundTripThroughSerializer_KeepCoreShape()
    {
        foreach (var sampleFlowPath in GetSampleFlowPaths())
        {
            var originalJson = File.ReadAllText(sampleFlowPath);
            var originalFlow = FlowSerializer.Deserialize(originalJson);
            Assert.NotNull(originalFlow);

            var roundTripJson = FlowSerializer.Serialize(originalFlow);
            var roundTripFlow = FlowSerializer.Deserialize(roundTripJson);
            Assert.NotNull(roundTripFlow);

            Assert.Equal(originalFlow.Id, roundTripFlow.Id);
            Assert.Equal(originalFlow.Name, roundTripFlow.Name);
            Assert.Equal(originalFlow.Version, roundTripFlow.Version);
            Assert.Equal(originalFlow.Nodes.Count, roundTripFlow.Nodes.Count);
            Assert.Equal(originalFlow.Connections.Count, roundTripFlow.Connections.Count);
            Assert.Equal(
                originalFlow.Nodes.Select(node => node.TypeId),
                roundTripFlow.Nodes.Select(node => node.TypeId));
            Assert.Equal(
                originalFlow.Connections.Select(connection => $"{connection.SourceNodeId}>{connection.TargetNodeId}"),
                roundTripFlow.Connections.Select(connection => $"{connection.SourceNodeId}>{connection.TargetNodeId}"));
        }
    }

    private static string[] GetSampleFlowPaths()
    {
        var sampleFlowsDirectory = GetSampleFlowsDirectory();
        Assert.True(Directory.Exists(sampleFlowsDirectory), $"Sample flows directory not found: {sampleFlowsDirectory}");
        return Directory.GetFiles(sampleFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly);
    }

    private static string GetSampleFlowsDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "flows"));
    }
}
