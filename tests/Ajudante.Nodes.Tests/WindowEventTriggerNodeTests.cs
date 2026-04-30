using System.Reflection;
using Ajudante.Nodes.Triggers;

namespace Ajudante.Nodes.Tests;

public class WindowEventTriggerNodeTests
{
    [Fact]
    public async Task StartAndStopWatching_TogglesUnderlyingWindowWatcher()
    {
        using var node = new WindowEventTriggerNode();
        node.Configure(new Dictionary<string, object?> { ["eventType"] = "Opened" });

        var watcher = GetPrivateField<object>(node, "_windowWatcher");
        Assert.False(GetPrivateField<bool>(watcher, "_running"));

        await node.StartWatchingAsync(CancellationToken.None);
        Assert.True(GetPrivateField<bool>(watcher, "_running"));

        await node.StopWatchingAsync();
        Assert.False(GetPrivateField<bool>(watcher, "_running"));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(instance)!;
    }
}
