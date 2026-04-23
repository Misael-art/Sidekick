using System.Diagnostics;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.browserOpenUrl",
    DisplayName = "Browser Open URL",
    Category = NodeCategory.Action,
    Description = "Opens a URL in the default browser",
    Color = "#22C55E")]
public class BrowserOpenUrlNode : IActionNode
{
    private string _url = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.browserOpenUrl",
        DisplayName = "Browser Open URL",
        Category = NodeCategory.Action,
        Description = "Opens a URL in the default browser",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "url", Name = "URL", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "url", Name = "URL", Type = PropertyType.String, DefaultValue = "", Description = "Target URL (supports {{variable}} templates)" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _url = NodeValueHelper.GetString(properties, "url");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedUrl = context.ResolveTemplate(_url);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
            return Task.FromResult(NodeResult.Fail("URL is required"));

        Process.Start(new ProcessStartInfo
        {
            FileName = resolvedUrl,
            UseShellExecute = true
        });

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["url"] = resolvedUrl
        }));
    }
}
