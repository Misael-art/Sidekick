using Ajudante.Core.Engine;
using Ajudante.Core.Registry;
using Ajudante.Core.Serialization;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class OfficialRecipesValidationTests
{
    [Fact]
    public void OfficialRecipeFlows_ValidateWithRealNodeRegistry()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(TextTemplateNode).Assembly);
        var validator = new FlowValidator(registry);

        var flowPaths = Directory
            .GetFiles(GetFlowsDirectory(), "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).StartsWith("recipe_", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).StartsWith("trae_", StringComparison.OrdinalIgnoreCase))
            .Where(IsFlowJsonFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(flowPaths);

        var failures = new List<string>();
        foreach (var path in flowPaths)
        {
            var flow = FlowSerializer.Deserialize(File.ReadAllText(path));
            Assert.NotNull(flow);

            var result = validator.Validate(flow);
            if (!result.IsValid)
                failures.Add($"{Path.GetFileName(path)}: {string.Join(" | ", result.Errors)}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void WhatsAppRecipe_ValidatesSuccessfullyWithoutUnguardedCycleWarning()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(TextTemplateNode).Assembly);
        var validator = new FlowValidator(registry);
        var flowPath = Path.Combine(GetFlowsDirectory(), "recipe_whatsapp_status_assistant.json");

        var flow = FlowSerializer.Deserialize(File.ReadAllText(flowPath));
        Assert.NotNull(flow);

        var result = validator.Validate(flow);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFlowJsonFile(string path)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("nodes", out var nodes)
                && nodes.ValueKind == System.Text.Json.JsonValueKind.Array
                && document.RootElement.TryGetProperty("connections", out var connections)
                && connections.ValueKind == System.Text.Json.JsonValueKind.Array;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string GetFlowsDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "flows"));
    }
}
