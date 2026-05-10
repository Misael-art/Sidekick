using Ajudante.Core.Engine;

namespace Ajudante.Core.Tests;

public class FlowVariableTemplateResolverTests
{
    [Fact]
    public void ResolveStringTemplate_ReplacesKnownVariables_FromFlowDefaults()
    {
        var flow = new Flow
        {
            Variables =
            {
                new FlowVariable { Name = "robloxPlayerExePath", Type = VariableType.String, Default = @"C:\Games\RobloxPlayerBeta.exe" },
                new FlowVariable { Name = "port", Type = VariableType.Integer, Default = 8080 }
            }
        };

        var resolved = FlowVariableTemplateResolver.ResolveStringTemplate(
            "path={{robloxPlayerExePath}} port={{port}}",
            flow);

        Assert.Equal(@"path=C:\Games\RobloxPlayerBeta.exe port=8080", resolved);
    }

    [Fact]
    public void ResolveStringTemplate_MissingVariable_BecomesEmptyString()
    {
        var flow = new Flow { Variables = { new FlowVariable { Name = "x", Type = VariableType.String, Default = "a" } } };

        var resolved = FlowVariableTemplateResolver.ResolveStringTemplate("{{x}}-{{missing}}-{{x}}", flow);

        Assert.Equal("a--a", resolved);
    }

    [Fact]
    public void ResolveStringTemplate_NoTemplates_ReturnsOriginal()
    {
        var flow = new Flow();
        const string plain = @"C:\No\Braces\Here.exe";
        Assert.Same(plain, FlowVariableTemplateResolver.ResolveStringTemplate(plain, flow));
    }

    [Fact]
    public void ResolvePropertyTemplates_ResolvesStringValuesWithTemplates()
    {
        var flow = new Flow
        {
            Variables =
            {
                new FlowVariable { Name = "p", Type = VariableType.String, Default = "resolved-path" }
            }
        };

        var props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["processPath"] = "{{p}}",
            ["processName"] = "MyApp",
            ["intervalMs"] = 500
        };

        var result = FlowVariableTemplateResolver.ResolvePropertyTemplates(flow, props);

        Assert.Equal("resolved-path", result["processPath"]);
        Assert.Equal("MyApp", result["processName"]);
        Assert.Equal(500, result["intervalMs"]);
    }
}
