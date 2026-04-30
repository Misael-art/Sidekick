using System.Reflection;
using Ajudante.Nodes.Triggers;

namespace Ajudante.Nodes.Tests;

public class ImageDetectedTriggerNodeTests
{
    [Fact]
    public void Configure_AcceptsStructuredSnipAssetTemplatePayload()
    {
        using var node = new ImageDetectedTriggerNode();
        var expectedBytes = CreateFakePngBytes();

        node.Configure(new Dictionary<string, object?>
        {
            ["templateImage"] = new Dictionary<string, object?>
            {
                ["kind"] = "snipAsset",
                ["assetId"] = "asset-123",
                ["displayName"] = "Header Button",
                ["imagePath"] = "assets/snips/asset-123.png",
                ["imageBase64"] = Convert.ToBase64String(expectedBytes)
            },
            ["threshold"] = 0.9,
            ["interval"] = 250
        });

        var field = typeof(ImageDetectedTriggerNode).GetField("_templateImage", BindingFlags.Instance | BindingFlags.NonPublic);
        var configuredBytes = Assert.IsType<byte[]>(field?.GetValue(node));

        Assert.Equal(expectedBytes, configuredBytes);
    }

    private static byte[] CreateFakePngBytes()
    {
        return
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 1, 0, 0, 0, 1,
            8, 6, 0, 0, 0, 31, 21, 196,
            137, 0, 0, 0, 12, 73, 68, 65,
            84, 120, 156, 99, 248, 15, 4, 0,
            9, 251, 3, 253, 160, 90, 121, 162,
            0, 0, 0, 0, 73, 69, 78, 68,
            174, 66, 96, 130
        ];
    }
}
