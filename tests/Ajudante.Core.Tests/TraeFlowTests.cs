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
        Assert.Equal(@"%LOCALAPPDATA%\Programs\Trae\Trae.exe", GetString(trigger.Properties, "processPath"));
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

    [Fact]
    public void RobloxPlaytimeRecipe_HasProcessTimerBlockAndSafetyGuards()
    {
        var flow = LoadFlow("recipe_roblox_playtime_limit.json");

        Assert.Equal("recipe-roblox-playtime-limit", flow.Id);
        Assert.Equal("Tempo de Jogo - ROBLOX", flow.Name);
        Assert.Contains(flow.Nodes, node => node.TypeId == "trigger.processEvent" && GetString(node.Properties, "processName") == "RobloxPlayerBeta");
        Assert.Contains(flow.Nodes, node => node.TypeId == "logic.cooldown");
        Assert.Contains(flow.Nodes, node => node.TypeId == "action.readState");
        Assert.Contains(flow.Nodes, node => node.TypeId == "action.persistState");
        Assert.Contains(flow.Nodes, node => node.TypeId == "logic.untilDateTime");
        Assert.Contains(flow.Nodes, node => node.TypeId == "action.overlayText" && GetString(node.Properties, "text").Contains("Seu tempo acabou", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(flow.Nodes, node => node.TypeId == "action.windowControl" && GetString(node.Properties, "operation") == "close");
        Assert.Contains(flow.Nodes, node => node.TypeId == "action.killProcess");
        Assert.Contains(flow.Nodes, node => node.Properties.ContainsKey("__ui.alias") && node.Properties.ContainsKey("__ui.comment"));
        Assert.Contains(flow.Annotations, sticky => sticky.Title.Contains("Como usar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(flow.Variables, variable => variable.Name == "tempoPermitidoMs" && variable.Type == VariableType.Integer);
    }

    private static Flow LoadTraeFlow()
    {
        return LoadFlow("trae_auto_continue.json");
    }

    private static Flow LoadFlow(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "flows", fileName));
        Assert.True(File.Exists(path), $"Flow not found at {path}");
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
