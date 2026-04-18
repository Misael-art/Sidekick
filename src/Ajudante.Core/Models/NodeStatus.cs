using System.Text.Json.Serialization;

namespace Ajudante.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeStatus
{
    Idle,
    Running,
    Completed,
    Error,
    Skipped
}
