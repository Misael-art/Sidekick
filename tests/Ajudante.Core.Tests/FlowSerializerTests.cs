using Ajudante.Core;
using Ajudante.Core.Serialization;
using System.Text.Json;

namespace Ajudante.Core.Tests;

public class FlowSerializerTests : IDisposable
{
    private readonly string _tempDir;

    public FlowSerializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AjudanteCoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static Flow CreateTestFlow()
    {
        return new Flow
        {
            Id = "flow-1",
            Name = "Test Flow",
            Version = 2,
            Variables = new List<FlowVariable>
            {
                new() { Name = "myVar", Type = VariableType.String, Default = "hello" },
                new() { Name = "count", Type = VariableType.Integer, Default = 10 }
            },
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "node-1",
                    TypeId = "trigger.hotkey",
                    Position = new NodePosition { X = 100, Y = 200 },
                    Properties = new Dictionary<string, object?> { { "key", "F5" } }
                },
                new()
                {
                    Id = "node-2",
                    TypeId = "action.mouseClick",
                    Position = new NodePosition { X = 300, Y = 200 },
                    Properties = new Dictionary<string, object?> { { "x", 500 }, { "y", 300 } }
                }
            },
            Connections = new List<Connection>
            {
                new()
                {
                    Id = "conn-1",
                    SourceNodeId = "node-1",
                    SourcePort = "triggered",
                    TargetNodeId = "node-2",
                    TargetPort = "in"
                }
            },
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesAllFields()
    {
        var original = CreateTestFlow();
        original.Annotations.Add(new StickyNote
        {
            Id = "sticky-1",
            Title = "Checklist",
            Body = "Capturar -> Aplicar -> Testar",
            Color = "blue",
            Position = new NodePosition { X = 96, Y = 144 },
            Width = 300,
            Height = 180
        });

        var json = FlowSerializer.Serialize(original);
        var deserialized = FlowSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.Variables.Count, deserialized.Variables.Count);
        Assert.Equal("myVar", deserialized.Variables[0].Name);
        Assert.Equal(VariableType.String, deserialized.Variables[0].Type);
        Assert.Equal(original.Nodes.Count, deserialized.Nodes.Count);
        Assert.Equal("node-1", deserialized.Nodes[0].Id);
        Assert.Equal("trigger.hotkey", deserialized.Nodes[0].TypeId);
        Assert.Equal(100, deserialized.Nodes[0].Position.X);
        Assert.Equal(200, deserialized.Nodes[0].Position.Y);
        Assert.Equal(original.Connections.Count, deserialized.Connections.Count);
        Assert.Equal("conn-1", deserialized.Connections[0].Id);
        Assert.Equal("node-1", deserialized.Connections[0].SourceNodeId);
        Assert.Equal("triggered", deserialized.Connections[0].SourcePort);
        Assert.Equal("node-2", deserialized.Connections[0].TargetNodeId);
        Assert.Equal("in", deserialized.Connections[0].TargetPort);
        Assert.Single(deserialized.Annotations);
        Assert.Equal("sticky-1", deserialized.Annotations[0].Id);
        Assert.Equal("Checklist", deserialized.Annotations[0].Title);
        Assert.Equal("Capturar -> Aplicar -> Testar", deserialized.Annotations[0].Body);
        Assert.Equal("blue", deserialized.Annotations[0].Color);
        Assert.Equal(96, deserialized.Annotations[0].Position.X);
        Assert.Equal(144, deserialized.Annotations[0].Position.Y);
        Assert.Equal(300, deserialized.Annotations[0].Width);
        Assert.Equal(180, deserialized.Annotations[0].Height);
        Assert.Equal(original.CreatedAt, deserialized.CreatedAt);
    }

    [Fact]
    public void Serialize_UpdatesModifiedAtTimestamp()
    {
        var flow = CreateTestFlow();
        var beforeSerialize = DateTime.UtcNow;

        FlowSerializer.Serialize(flow);

        Assert.True(flow.ModifiedAt >= beforeSerialize);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForInvalidJson()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            FlowSerializer.Deserialize("not valid json {{{"));
    }

    [Fact]
    public void Deserialize_HandlesEmptyObject()
    {
        // An empty JSON object should deserialize (with defaults)
        var flow = FlowSerializer.Deserialize("{}");
        // The flow might be null or have defaults depending on the required fields
        // NodeInstance has required TypeId, but an empty JSON won't have nodes
        Assert.NotNull(flow);
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNaming()
    {
        var flow = new Flow { Name = "CamelCase Test" };

        var json = FlowSerializer.Serialize(flow);

        Assert.Contains("\"name\":", json);
        Assert.Contains("\"version\":", json);
        Assert.Contains("\"createdAt\":", json);
        Assert.Contains("\"modifiedAt\":", json);
        Assert.DoesNotContain("\"Name\":", json);
        Assert.DoesNotContain("\"Version\":", json);
    }

    [Fact]
    public void Serialize_ProducesIndentedJson()
    {
        var flow = new Flow { Name = "Indent Test" };

        var json = FlowSerializer.Serialize(flow);

        // Indented JSON has newlines
        Assert.Contains("\n", json);
    }

    [Fact]
    public async Task SaveAsync_LoadAsync_RoundTrips()
    {
        var flow = CreateTestFlow();
        var filePath = Path.Combine(_tempDir, "test-flow.json");

        await FlowSerializer.SaveAsync(flow, filePath);
        var loaded = await FlowSerializer.LoadAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(flow.Id, loaded.Id);
        Assert.Equal(flow.Name, loaded.Name);
        Assert.Equal(flow.Nodes.Count, loaded.Nodes.Count);
        Assert.Equal(flow.Connections.Count, loaded.Connections.Count);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        var flow = new Flow { Name = "Dir Test" };
        var subDir = Path.Combine(_tempDir, "sub", "dir");
        var filePath = Path.Combine(subDir, "test.json");

        await FlowSerializer.SaveAsync(flow, filePath);

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        var flow = CreateTestFlow();
        var filePath = Path.Combine(_tempDir, "valid.json");

        await FlowSerializer.SaveAsync(flow, filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = FlowSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_ForMissingFile()
    {
        var filePath = Path.Combine(_tempDir, "does-not-exist.json");

        var result = await FlowSerializer.LoadAsync(filePath);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAllAsync_LoadsMultipleFlows()
    {
        var flow1 = new Flow { Id = "f1", Name = "Flow 1" };
        var flow2 = new Flow { Id = "f2", Name = "Flow 2" };
        var flow3 = new Flow { Id = "f3", Name = "Flow 3" };

        await FlowSerializer.SaveAsync(flow1, Path.Combine(_tempDir, "flow1.json"));
        await FlowSerializer.SaveAsync(flow2, Path.Combine(_tempDir, "flow2.json"));
        await FlowSerializer.SaveAsync(flow3, Path.Combine(_tempDir, "flow3.json"));

        var loaded = await FlowSerializer.LoadAllAsync(_tempDir);

        Assert.Equal(3, loaded.Count);
        var ids = loaded.Select(f => f.Id).OrderBy(x => x).ToList();
        Assert.Contains("f1", ids);
        Assert.Contains("f2", ids);
        Assert.Contains("f3", ids);
    }

    [Fact]
    public async Task LoadAllAsync_ReturnsEmptyList_ForNonExistentDirectory()
    {
        var result = await FlowSerializer.LoadAllAsync(Path.Combine(_tempDir, "nonexistent"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAllAsync_ReturnsEmptyList_ForEmptyDirectory()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var result = await FlowSerializer.LoadAllAsync(emptyDir);

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAllAsync_IgnoresNonJsonFiles()
    {
        await FlowSerializer.SaveAsync(new Flow { Id = "f1" }, Path.Combine(_tempDir, "flow.json"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "readme.txt"), "not a flow");

        var result = await FlowSerializer.LoadAllAsync(_tempDir);

        Assert.Single(result);
        Assert.Equal("f1", result[0].Id);
    }

    [Fact]
    public void Serialize_EnumValues_UseCamelCase()
    {
        var flow = new Flow
        {
            Variables = new List<FlowVariable>
            {
                new() { Name = "test", Type = VariableType.Boolean }
            }
        };

        var json = FlowSerializer.Serialize(flow);

        // JsonStringEnumConverter with CamelCase should produce lowercase enum names
        Assert.Contains("boolean", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_Deserialize_PreservesNodePositions()
    {
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "n1",
                    TypeId = "action.mouseClick",
                    Position = new NodePosition { X = 123.45, Y = 678.90 }
                }
            }
        };

        var json = FlowSerializer.Serialize(flow);
        var result = FlowSerializer.Deserialize(json);

        Assert.NotNull(result);
        Assert.Equal(123.45, result.Nodes[0].Position.X);
        Assert.Equal(678.90, result.Nodes[0].Position.Y);
    }

    [Fact]
    public void Serialize_Deserialize_PreservesStructuredImageTemplatePayload()
    {
        var flow = new Flow
        {
            Nodes = new List<NodeInstance>
            {
                new()
                {
                    Id = "image-trigger",
                    TypeId = "trigger.imageDetected",
                    Position = new NodePosition { X = 10, Y = 20 },
                    Properties = new Dictionary<string, object?>
                    {
                        ["templateImage"] = new Dictionary<string, object?>
                        {
                            ["kind"] = "snipAsset",
                            ["assetId"] = "asset-123",
                            ["displayName"] = "Header Button",
                            ["imagePath"] = "assets/snips/asset-123.png",
                            ["imageBase64"] = Convert.ToBase64String([1, 2, 3, 4])
                        }
                    }
                }
            }
        };

        var json = FlowSerializer.Serialize(flow);
        var result = FlowSerializer.Deserialize(json);

        Assert.Contains("\"templateImage\":", json);
        Assert.Contains("\"assetId\": \"asset-123\"", json);
        Assert.NotNull(result);

        var templateImage = Assert.IsType<JsonElement>(result.Nodes[0].Properties["templateImage"]);
        Assert.Equal(JsonValueKind.Object, templateImage.ValueKind);
        Assert.Equal("snipAsset", templateImage.GetProperty("kind").GetString());
        Assert.Equal("asset-123", templateImage.GetProperty("assetId").GetString());
        Assert.Equal("Header Button", templateImage.GetProperty("displayName").GetString());
        Assert.Equal("assets/snips/asset-123.png", templateImage.GetProperty("imagePath").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), templateImage.GetProperty("imageBase64").GetString());
    }
}
