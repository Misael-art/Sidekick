using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajudante.App.Bridge;

/// <summary>
/// Message protocol DTO for communication between the React frontend and the C# backend
/// via WebView2's web message channel.
/// </summary>
public class BridgeMessage
{
    /// <summary>
    /// Message type: "command" (JS to C#), "event" (C# to JS), or "response" (C# reply to command).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// Routing channel: "flow", "engine", "platform", "inspector", "registry", "assets".
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    /// <summary>
    /// The action to perform: "saveFlow", "runFlow", "getNodeDefinitions", etc.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    /// <summary>
    /// Correlation ID used to match responses to requests.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    /// <summary>
    /// Arbitrary JSON payload. Interpretation depends on channel + action.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    /// <summary>
    /// Error message, populated only in response messages when something goes wrong.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public static class Types
    {
        public const string Command = "command";
        public const string Event = "event";
        public const string Response = "response";
    }

    public static class Channels
    {
        public const string Flow = "flow";
        public const string Engine = "engine";
        public const string Platform = "platform";
        public const string Inspector = "inspector";
        public const string Registry = "registry";
        public const string Assets = "assets";
    }
}
