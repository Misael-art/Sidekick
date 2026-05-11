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

        var sampleFlowPaths = GetSampleFlowPaths();
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

    [Fact]
    public void SampleFlows_WhatsAppRecipeUsesPortugueseMiraSelectorsAndRefreshVersion()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected WhatsApp recipe flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        Assert.Equal("Recipe - WhatsApp Status Assistant (Draft Safe)", flow.Name);
        Assert.True(flow.Version >= 8, "WhatsApp recipe must be versioned above stale v7 AppData copies.");

        var searchNode = flow.Nodes.Single(node => node.Id == "type-owner-phone");
        Assert.Equal("{{whatsappSearchPlaceholder}}", Assert.IsType<JsonElement>(searchNode.Properties["elementName"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(searchNode.Properties["elementNameMatch"]).GetString());
        Assert.Equal("Edit", Assert.IsType<JsonElement>(searchNode.Properties["controlType"]).GetString());
        Assert.Contains(flow.Variables, variable =>
            variable.Name == "whatsappSearchPlaceholder" &&
            variable.Default?.ToString() == "Pesquisar ou começar uma nova conversa");

        var typeMenuNode = flow.Nodes.Single(node => node.Id == "type-menu");
        Assert.Equal("{{whatsappOwnerComposerHint}}", Assert.IsType<JsonElement>(typeMenuNode.Properties["elementName"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(typeMenuNode.Properties["elementNameMatch"]).GetString());
        Assert.Contains(flow.Variables, variable =>
            variable.Name == "whatsappComposerHint" &&
            variable.Default?.ToString() == "Digite uma mensagem");
        Assert.Contains(flow.Variables, variable =>
            variable.Name == "whatsappOwnerComposerHint" &&
            variable.Default?.ToString()?.Contains("Papai Oliveira Novo", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(flow.Variables, variable =>
            variable.Name == "whatsappChatHeaderHint" &&
            variable.Default?.ToString()?.Contains("Mensagens para mim", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "focus-existing-whatsapp" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "open-whatsapp");
    }

    [Fact]
    public void SampleFlows_WhatsAppRecipeClicksContactDirectlyWithKeyboardFallback()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected WhatsApp recipe flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var clickContact = flow.Nodes.Single(node => node.Id == "click-contact-result");
        Assert.Equal("action.desktopClickElement", clickContact.TypeId);
        Assert.Equal("{{whatsappOwnerContactName}}", Assert.IsType<JsonElement>(clickContact.Properties["elementName"]).GetString());
        Assert.Equal("DataItem", Assert.IsType<JsonElement>(clickContact.Properties["controlType"]).GetString());
        Assert.False(Assert.IsType<JsonElement>(clickContact.Properties["restoreWindowBeforeFallback"]).GetBoolean());
        Assert.Equal("{{whatsappSearchPlaceholder}}", Assert.IsType<JsonElement>(clickContact.Properties["fallbackAnchorElementName"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(clickContact.Properties["fallbackAnchorElementNameMatch"]).GetString());
        Assert.Equal("Edit", Assert.IsType<JsonElement>(clickContact.Properties["fallbackAnchorControlType"]).GetString());
        Assert.True(Assert.IsType<JsonElement>(clickContact.Properties["fallbackAnchorOffsetX"]).GetInt32() > 0);
        Assert.True(Assert.IsType<JsonElement>(clickContact.Properties["fallbackAnchorOffsetY"]).GetInt32() >= 170);

        var selectFirstResult = flow.Nodes.Single(node => node.Id == "select-first-result");
        Assert.Equal("action.keyboardPress", selectFirstResult.TypeId);
        Assert.Equal("Down", Assert.IsType<JsonElement>(selectFirstResult.Properties["key"]).GetString());

        var openSelectedChat = flow.Nodes.Single(node => node.Id == "open-selected-chat");
        Assert.Equal("action.keyboardPress", openSelectedChat.TypeId);
        Assert.Equal("Return", Assert.IsType<JsonElement>(openSelectedChat.Properties["key"]).GetString());

        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "delay-search-results" &&
            connection.TargetNodeId == "click-contact-result");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "click-contact-result" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "select-first-result");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "click-contact-result" &&
            connection.SourcePort == "out" &&
            connection.TargetNodeId == "wait-owner-chat-header-after-click");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "select-first-result" &&
            connection.TargetNodeId == "delay-after-result-selection");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "open-selected-chat" &&
            connection.TargetNodeId == "wait-owner-chat-header");
        Assert.DoesNotContain(flow.Nodes, node => node.Id == "select-chat-by-enter");
    }

    [Fact]
    public void SampleFlows_WhatsAppRecipeValidatesOwnerChatBeforeTypingMenu()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var headerAfterClick = flow.Nodes.Single(node => node.Id == "wait-owner-chat-header-after-click");
        Assert.Equal("action.browserWaitElement", headerAfterClick.TypeId);
        Assert.Equal("{{whatsappChatHeaderHint}}", Assert.IsType<JsonElement>(headerAfterClick.Properties["elementName"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(headerAfterClick.Properties["elementNameMatch"]).GetString());
        Assert.Equal("Button", Assert.IsType<JsonElement>(headerAfterClick.Properties["controlType"]).GetString());

        var headerAfterKeyboard = flow.Nodes.Single(node => node.Id == "wait-owner-chat-header");
        Assert.Equal("{{whatsappChatHeaderHint}}", Assert.IsType<JsonElement>(headerAfterKeyboard.Properties["elementName"]).GetString());
        Assert.Equal("Button", Assert.IsType<JsonElement>(headerAfterKeyboard.Properties["controlType"]).GetString());

        var waitComposer = flow.Nodes.Single(node => node.Id == "wait-composer");
        Assert.Equal("{{whatsappOwnerComposerHint}}", Assert.IsType<JsonElement>(waitComposer.Properties["elementName"]).GetString());
        var waitComposerAfterClick = flow.Nodes.Single(node => node.Id == "wait-composer-after-click");
        Assert.Equal("{{whatsappOwnerComposerHint}}", Assert.IsType<JsonElement>(waitComposerAfterClick.Properties["elementName"]).GetString());

        var typeMenu = flow.Nodes.Single(node => node.Id == "type-menu");
        Assert.Equal("{{whatsappOwnerComposerHint}}", Assert.IsType<JsonElement>(typeMenu.Properties["elementName"]).GetString());
        var typeResponse = flow.Nodes.Single(node => node.Id == "type-response");
        Assert.Equal("{{whatsappOwnerComposerHint}}", Assert.IsType<JsonElement>(typeResponse.Properties["elementName"]).GetString());

        Assert.DoesNotContain(flow.Connections, connection =>
            connection.SourceNodeId == "click-contact-result" &&
            connection.TargetNodeId == "wait-composer-after-click");
        Assert.DoesNotContain(flow.Connections, connection =>
            connection.SourceNodeId == "open-selected-chat" &&
            connection.TargetNodeId == "wait-composer");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-owner-chat-header-after-click" &&
            connection.SourcePort == "out" &&
            connection.TargetNodeId == "wait-composer-after-click");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-owner-chat-header-after-click" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "select-first-result");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-owner-chat-header" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "log-contact-not-found");
    }

    [Fact]
    public void SampleFlows_WhatsAppRecipeUsesChatTranscriptAnchorForIncomingMessages()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        Assert.Contains(flow.Variables, variable =>
            variable.Name == "whatsappChatAreaHint" &&
            variable.Default?.ToString() == "Hoje");

        var watcher = flow.Nodes.Single(node => node.Id == "watch-message");
        Assert.Equal("{{whatsappChatAreaHint}}", Assert.IsType<JsonElement>(watcher.Properties["elementName"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(watcher.Properties["elementNameMatch"]).GetString());
        Assert.Equal("Group", Assert.IsType<JsonElement>(watcher.Properties["controlType"]).GetString());
    }

    [Fact]
    public void SampleFlows_WhatsAppRecipeRefreshesEdgeWhenInitialLoadStalls()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var retry = flow.Nodes.Single(node => node.Id == "refresh-whatsapp-attempt");
        Assert.Equal("logic.retryFlow", retry.TypeId);
        Assert.Equal("whatsappRefreshAttempts", Assert.IsType<JsonElement>(retry.Properties["counterVariable"]).GetString());
        Assert.Equal(3, Assert.IsType<JsonElement>(retry.Properties["maxAttempts"]).GetInt32());

        var refreshKey = flow.Nodes.Single(node => node.Id == "refresh-whatsapp-key");
        Assert.Equal("action.keyboardPress", refreshKey.TypeId);
        Assert.Equal("R", Assert.IsType<JsonElement>(refreshKey.Properties["key"]).GetString());
        Assert.Equal("Ctrl", Assert.IsType<JsonElement>(refreshKey.Properties["modifiers"]).GetString());

        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-login" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "refresh-whatsapp-attempt");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "refresh-whatsapp-attempt" &&
            connection.SourcePort == "retry" &&
            connection.TargetNodeId == "log-refresh-whatsapp");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "refresh-whatsapp-attempt" &&
            connection.SourcePort == "giveUp" &&
            connection.TargetNodeId == "log-login-required");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-login-retry" &&
            connection.SourcePort == "out" &&
            connection.TargetNodeId == "type-owner-phone");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "wait-login-retry" &&
            connection.SourcePort == "notFound" &&
            connection.TargetNodeId == "refresh-whatsapp-attempt");
    }

    [Fact]
    public void SampleFlows_WhatsAppRecipePreparesEdgeWindowBeforeSearching()
    {
        var sampleFlowPath = Path.Combine(GetSampleFlowsDirectory(), "recipe_whatsapp_status_assistant.json");
        Assert.True(File.Exists(sampleFlowPath), $"Expected WhatsApp recipe flow was not found: {sampleFlowPath}");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(sampleFlowPath));
        Assert.NotNull(flow);

        var maximizeNode = flow.Nodes.Single(node => node.Id == "maximize-whatsapp-window");
        Assert.Equal("action.windowControl", maximizeNode.TypeId);
        Assert.Equal("maximize", Assert.IsType<JsonElement>(maximizeNode.Properties["operation"]).GetString());
        Assert.Equal("{{whatsappWindowTitle}}", Assert.IsType<JsonElement>(maximizeNode.Properties["windowTitle"]).GetString());
        Assert.Equal("contains", Assert.IsType<JsonElement>(maximizeNode.Properties["windowTitleMatch"]).GetString());

        var dismissRestorePrompt = flow.Nodes.Single(node => node.Id == "dismiss-edge-restore-prompt");
        Assert.Equal("action.keyboardPress", dismissRestorePrompt.TypeId);
        Assert.Equal("Escape", Assert.IsType<JsonElement>(dismissRestorePrompt.Properties["key"]).GetString());

        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "focus-existing-whatsapp" &&
            connection.SourcePort == "out" &&
            connection.TargetNodeId == "maximize-whatsapp-window");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "open-whatsapp" &&
            connection.TargetNodeId == "delay-after-open-whatsapp");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "delay-after-open-whatsapp" &&
            connection.TargetNodeId == "maximize-whatsapp-window");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "maximize-whatsapp-window" &&
            connection.SourcePort == "out" &&
            connection.TargetNodeId == "dismiss-edge-restore-prompt");
        Assert.Contains(flow.Connections, connection =>
            connection.SourceNodeId == "dismiss-edge-restore-prompt" &&
            connection.TargetNodeId == "wait-login");
    }

    private static string[] GetSampleFlowPaths()
    {
        var sampleFlowsDirectory = GetSampleFlowsDirectory();
        Assert.True(Directory.Exists(sampleFlowsDirectory), $"Sample flows directory not found: {sampleFlowsDirectory}");
        return Directory
            .GetFiles(sampleFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(IsFlowJsonFile)
            .ToArray();
    }

    private static bool IsFlowJsonFile(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("nodes", out var nodes)
                && nodes.ValueKind == JsonValueKind.Array
                && document.RootElement.TryGetProperty("connections", out var connections)
                && connections.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetSampleFlowsDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "flows"));
    }
}
