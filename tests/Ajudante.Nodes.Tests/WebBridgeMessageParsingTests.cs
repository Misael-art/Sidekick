using Ajudante.App.Bridge;

namespace Ajudante.Nodes.Tests;

public class WebBridgeMessageParsingTests
{
    [Fact]
    public void DeserializeIncomingMessage_AcceptsJsonObjectPayload()
    {
        const string rawJson = """
        {"type":"command","channel":"flow","action":"newFlow","requestId":"req-1","payload":{"name":"Untitled Flow"}}
        """;

        var message = WebBridge.DeserializeIncomingMessage(rawJson);

        Assert.NotNull(message);
        Assert.Equal("command", message!.Type);
        Assert.Equal("flow", message.Channel);
        Assert.Equal("newFlow", message.Action);
        Assert.Equal("req-1", message.RequestId);
        Assert.True(message.Payload.HasValue);
        Assert.Equal("Untitled Flow", message.Payload.Value.GetProperty("name").GetString());
    }

    [Fact]
    public void DeserializeIncomingMessage_AcceptsJsonStringPayload()
    {
        const string rawJson = """
        "{\"type\":\"command\",\"channel\":\"engine\",\"action\":\"runFlow\",\"requestId\":\"req-2\",\"payload\":{\"id\":\"flow-1\"}}"
        """;

        var message = WebBridge.DeserializeIncomingMessage(rawJson);

        Assert.NotNull(message);
        Assert.Equal("command", message!.Type);
        Assert.Equal("engine", message.Channel);
        Assert.Equal("runFlow", message.Action);
        Assert.Equal("req-2", message.RequestId);
        Assert.True(message.Payload.HasValue);
        Assert.Equal("flow-1", message.Payload.Value.GetProperty("id").GetString());
    }
}
