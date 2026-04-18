using System.Diagnostics;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.openProgram",
    DisplayName = "Open Program",
    Category = NodeCategory.Action,
    Description = "Launches an application or opens a file",
    Color = "#22C55E")]
public class OpenProgramNode : IActionNode
{
    private string _path = "";
    private string _arguments = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.openProgram",
        DisplayName = "Open Program",
        Category = NodeCategory.Action,
        Description = "Launches an application or opens a file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "path",
                Name = "Program Path",
                Type = PropertyType.FilePath,
                DefaultValue = "",
                Description = "Path to the program or file to open"
            },
            new()
            {
                Id = "arguments",
                Name = "Arguments",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "Command-line arguments (supports {{variable}} templates)"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("path", out var p) && p is string path)
            _path = path;
        if (properties.TryGetValue("arguments", out var a) && a is string args)
            _arguments = args;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_path))
                return Task.FromResult(NodeResult.Fail("Program path is required"));

            var resolvedPath = context.ResolveTemplate(_path);
            var resolvedArgs = context.ResolveTemplate(_arguments);

            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                Arguments = resolvedArgs,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["processId"] = process?.Id,
                ["processName"] = process?.ProcessName,
                ["path"] = resolvedPath
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Failed to open program: {ex.Message}"));
        }
    }
}
