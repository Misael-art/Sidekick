using Ajudante.Core.Serialization;

namespace Ajudante.Core.Tests;

public class TraeFlowTests
{
    [Fact]
    public void TraeAutoContinueFlow_UsesRealDesktopTrigger_NotManualStart()
    {
        var flow = LoadTraeFlow();

        Assert.Equal("trae-auto-continue", flow.Id);
        Assert.DoesNotContain(flow.Nodes, node => node.TypeId == "trigger.manualStart");
        Assert.Contains(flow.Nodes, node => node.Id == "watch-continue" && node.TypeId == "trigger.desktopElementAppeared");
    }

    [Fact]
    public void TraeAutoContinueFlow_HasProcessScopedSelectorAndLoopGuards()
    {
        var flow = LoadTraeFlow();
        var trigger = flow.Nodes.Single(node => node.Id == "watch-continue");

        Assert.Equal("contains", GetString(trigger.Properties, "windowTitleMatch"));
        Assert.Equal("Trae", GetString(trigger.Properties, "processName"));
        Assert.Equal(@"C:\Users\misae\AppData\Local\Programs\Trae\Trae.exe", GetString(trigger.Properties, "processPath"));
        Assert.Equal("Continue", GetString(trigger.Properties, "elementName"));
        Assert.Equal("button", GetString(trigger.Properties, "controlType"));
        Assert.Equal(7000, GetInt(trigger.Properties, "cooldownMs"));
        Assert.Equal(500, GetInt(trigger.Properties, "debounceMs"));
        Assert.Equal(20, GetInt(trigger.Properties, "maxRepeat"));
    }

    [Fact]
    public void TraeAutoContinueFlow_ClicksWithDesktopActionAndHasNotFoundPath()
    {
        var flow = LoadTraeFlow();

        Assert.Contains(flow.Nodes, node => node.Id == "click-continue" && node.TypeId == "action.desktopClickElement");
        Assert.Contains(flow.Connections, connection => connection.SourceNodeId == "click-continue" && connection.SourcePort == "notFound");
        Assert.Contains(flow.Variables, variable => variable.Name == "traeProcessPath" && variable.Type == VariableType.String);
    }

    private static Flow LoadTraeFlow()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "flows", "trae_auto_continue.json"));
        Assert.True(File.Exists(path), $"Trae flow not found at {path}");
        return FlowSerializer.Deserialize(File.ReadAllText(path))!;
    }

    private static string GetString(Dictionary<string, object?> properties, string key)
    {
        Assert.True(properties.TryGetValue(key, out var value), $"Missing property {key}");
        return value switch
        {
            System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String => element.GetString() ?? "",
            _ => value?.ToString() ?? ""
        };
    }

    private static int GetInt(Dictionary<string, object?> properties, string key)
    {
        Assert.True(properties.TryGetValue(key, out var value), $"Missing property {key}");
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            System.Text.Json.JsonElement element when element.TryGetInt32(out var parsed) => parsed,
            _ => int.Parse(value?.ToString() ?? "0")
        };
    }
}
