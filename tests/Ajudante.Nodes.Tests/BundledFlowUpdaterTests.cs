using Ajudante.App.Bridge;
using Ajudante.Core;
using System.Text.Json;

namespace Ajudante.Nodes.Tests;

public class BundledFlowUpdaterTests
{
    [Fact]
    public void RefreshFromSeed_ReplacesStaleNativeGraphAndPreservesVariableValues()
    {
        var stale = new Flow
        {
            Id = "recipe-whatsapp-status-assistant",
            Name = "Recipe - WhatsApp Assistant v2",
            Version = 2,
            Variables =
            {
                new FlowVariable { Name = "whatsappOwnerPhone", Type = VariableType.String, Default = "5511999999999" }
            },
            Nodes =
            {
                new NodeInstance
                {
                    Id = "type-owner-phone",
                    TypeId = "action.browserType",
                    Properties = { ["elementName"] = "Search" }
                }
            }
        };
        var seed = new Flow
        {
            Id = "recipe-whatsapp-status-assistant",
            Name = "Recipe - WhatsApp Status Assistant (Draft Safe)",
            Version = 3,
            Variables =
            {
                new FlowVariable { Name = "whatsappOwnerPhone", Type = VariableType.String, Default = "5511959766061" },
                new FlowVariable { Name = "whatsappSearchPlaceholder", Type = VariableType.String, Default = "Pesquisar ou começar uma nova conversa" }
            },
            Nodes =
            {
                new NodeInstance
                {
                    Id = "type-owner-phone",
                    TypeId = "action.browserType",
                    Properties = { ["elementName"] = "{{whatsappSearchPlaceholder}}" }
                }
            }
        };

        var refreshed = BundledFlowUpdater.RefreshFromSeedIfNewer(stale, seed);

        Assert.True(refreshed);
        Assert.Equal(3, stale.Version);
        Assert.Equal("Recipe - WhatsApp Status Assistant (Draft Safe)", stale.Name);
        Assert.Equal("{{whatsappSearchPlaceholder}}", Assert.IsType<JsonElement>(stale.Nodes.Single().Properties["elementName"]).GetString());
        Assert.Equal("5511999999999", GetStringDefault(stale.Variables.Single(v => v.Name == "whatsappOwnerPhone")));
        Assert.Equal("Pesquisar ou começar uma nova conversa", GetStringDefault(stale.Variables.Single(v => v.Name == "whatsappSearchPlaceholder")));
    }

    private static string? GetStringDefault(FlowVariable variable)
    {
        return variable.Default switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => variable.Default?.ToString()
        };
    }
}
