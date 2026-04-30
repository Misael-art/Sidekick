using Ajudante.Core.Serialization;
using System.Text.Json;

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
        var hasContinuousTriggerSample = false;

        foreach (var sampleFlowPath in sampleFlowPaths)
        {
            var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
            Assert.NotNull(flow);

            Assert.False(string.IsNullOrWhiteSpace(flow.Id), $"Sample flow '{sampleFlowPath}' is missing an id.");
            Assert.True(seenIds.Add(flow.Id), $"Duplicate sample flow id '{flow.Id}' found.");
            Assert.False(string.IsNullOrWhiteSpace(flow.Name), $"Sample flow '{sampleFlowPath}' is missing a name.");
            Assert.NotEmpty(flow.Nodes);
            Assert.Contains(flow.Nodes, node => node.TypeId.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase));

            hasContinuousTriggerSample |= flow.Nodes.Any(node =>
                node.TypeId.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.TypeId, "trigger.manualStart", StringComparison.OrdinalIgnoreCase));
        }

        Assert.True(hasContinuousTriggerSample, "At least one sample flow should demonstrate a continuous trigger.");
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

    [Fact]
    public void SampleFlows_IncludeStructuredSnipReuseExample()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "portfolio_snip_reuse_demo.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected Snip reuse sample flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var imageTrigger = flow.Nodes.Single(node => node.TypeId == "trigger.imageDetected");
        var templateImage = Assert.IsType<JsonElement>(imageTrigger.Properties["templateImage"]);

        Assert.Equal(JsonValueKind.Object, templateImage.ValueKind);
        Assert.Equal("snipAsset", templateImage.GetProperty("kind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(templateImage.GetProperty("assetId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(templateImage.GetProperty("displayName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(templateImage.GetProperty("imagePath").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(templateImage.GetProperty("imageBase64").GetString()));
    }

    [Fact]
    public void SampleFlows_IncludeBrowserMiraSelectorExample()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "portfolio_browser_mira_demo.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected browser Mira sample flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var waitNode = flow.Nodes.Single(node => node.TypeId == "action.browserWaitElement");
        var clickNode = flow.Nodes.Single(node => node.TypeId == "action.browserClick");

        Assert.Equal("Portal", Assert.IsType<JsonElement>(waitNode.Properties["windowTitle"]).GetString());
        Assert.Equal("search-box", Assert.IsType<JsonElement>(waitNode.Properties["automationId"]).GetString());
        Assert.Equal("Search", Assert.IsType<JsonElement>(waitNode.Properties["elementName"]).GetString());
        Assert.Equal("Edit", Assert.IsType<JsonElement>(waitNode.Properties["controlType"]).GetString());
        Assert.Equal(5000, Assert.IsType<JsonElement>(waitNode.Properties["timeoutMs"]).GetInt32());

        Assert.Equal("Portal", Assert.IsType<JsonElement>(clickNode.Properties["windowTitle"]).GetString());
        Assert.Equal("search-box", Assert.IsType<JsonElement>(clickNode.Properties["automationId"]).GetString());
        Assert.Equal("Search", Assert.IsType<JsonElement>(clickNode.Properties["elementName"]).GetString());
        Assert.Equal("Edit", Assert.IsType<JsonElement>(clickNode.Properties["controlType"]).GetString());
        Assert.Equal("single", Assert.IsType<JsonElement>(clickNode.Properties["clickType"]).GetString());
    }

    [Fact]
    public void SampleFlows_IncludeBrowserMiraTextExample()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "portfolio_browser_mira_text_demo.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected browser Mira text sample flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var typeNode = flow.Nodes.Single(node => node.TypeId == "action.browserType");
        var extractNode = flow.Nodes.Single(node => node.TypeId == "action.browserExtractText");

        Assert.Equal("Portal", Assert.IsType<JsonElement>(typeNode.Properties["windowTitle"]).GetString());
        Assert.Equal("search-box", Assert.IsType<JsonElement>(typeNode.Properties["automationId"]).GetString());
        Assert.Equal("Search", Assert.IsType<JsonElement>(typeNode.Properties["elementName"]).GetString());
        Assert.Equal("Edit", Assert.IsType<JsonElement>(typeNode.Properties["controlType"]).GetString());
        Assert.Equal("status:open", Assert.IsType<JsonElement>(typeNode.Properties["text"]).GetString());
        Assert.True(Assert.IsType<JsonElement>(typeNode.Properties["clearExisting"]).GetBoolean());

        Assert.Equal("Portal", Assert.IsType<JsonElement>(extractNode.Properties["windowTitle"]).GetString());
        Assert.Equal("result-count", Assert.IsType<JsonElement>(extractNode.Properties["automationId"]).GetString());
        Assert.Equal("Results", Assert.IsType<JsonElement>(extractNode.Properties["elementName"]).GetString());
        Assert.Equal("Text", Assert.IsType<JsonElement>(extractNode.Properties["controlType"]).GetString());
        Assert.Equal("resultCount", Assert.IsType<JsonElement>(extractNode.Properties["storeInVariable"]).GetString());
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
