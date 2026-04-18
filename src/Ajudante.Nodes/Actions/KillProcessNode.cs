using System.Diagnostics;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.killProcess",
    DisplayName = "Kill Process",
    Category = NodeCategory.Action,
    Description = "Terminates a running process by name",
    Color = "#22C55E")]
public class KillProcessNode : IActionNode
{
    private string _processName = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.killProcess",
        DisplayName = "Kill Process",
        Category = NodeCategory.Action,
        Description = "Terminates a running process by name",
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
                Id = "processName",
                Name = "Process Name",
                Type = PropertyType.String,
                DefaultValue = "",
                Description = "Name of the process to kill (without .exe extension)"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("processName", out var pn) && pn is string name)
            _processName = name;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_processName))
                return Task.FromResult(NodeResult.Fail("Process name is required"));

            var resolvedName = context.ResolveTemplate(_processName);
            var processes = Process.GetProcessesByName(resolvedName);
            var killedCount = 0;

            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    killedCount++;
                }
                catch
                {
                    // Process may have already exited
                }
                finally
                {
                    process.Dispose();
                }
            }

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["processName"] = resolvedName,
                ["killedCount"] = killedCount
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Failed to kill process: {ex.Message}"));
        }
    }
}
