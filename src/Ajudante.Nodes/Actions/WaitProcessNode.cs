using System.Diagnostics;
using System.IO;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.waitProcess",
    DisplayName = "Wait Process",
    Category = NodeCategory.Action,
    Description = "Waits for a Windows process to start or stop",
    Color = "#22C55E")]
public class WaitProcessNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.waitProcess",
        DisplayName = "Wait Process",
        Category = NodeCategory.Action,
        Description = "Waits for a Windows process to start or stop",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Matched", DataType = PortDataType.Flow },
            new() { Id = "timeout", Name = "Timeout", DataType = PortDataType.Flow },
            new() { Id = "processId", Name = "Process ID", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "", Description = "Process name, with or without .exe" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Optional full executable path" },
            new() { Id = "state", Name = "State", Type = PropertyType.Dropdown, DefaultValue = "started", Description = "Wait for process start or stop", Options = new[] { "started", "stopped" } },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 30000, Description = "Maximum wait time" },
            new() { Id = "intervalMs", Name = "Poll Interval (ms)", Type = PropertyType.Integer, DefaultValue = 500, Description = "Delay between checks" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var processName = NormalizeProcessName(context.ResolveTemplate(NodeValueHelper.GetString(_properties, "processName")));
        var processPath = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "processPath"));
        var state = NodeValueHelper.GetString(_properties, "state", "started");
        var timeoutMs = Math.Max(0, NodeValueHelper.GetInt(_properties, "timeoutMs", 30000));
        var intervalMs = Math.Max(100, NodeValueHelper.GetInt(_properties, "intervalMs", 500));
        var startedAt = Environment.TickCount64;

        do
        {
            ct.ThrowIfCancellationRequested();
            var process = FindProcess(processName, processPath);
            var matched = string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase)
                ? process is null
                : process is not null;

            if (matched)
            {
                return NodeResult.Ok("out", new Dictionary<string, object?>
                {
                    ["processId"] = process?.Id ?? 0,
                    ["state"] = state
                });
            }

            if (timeoutMs <= 0)
                break;

            await Task.Delay(intervalMs, ct);
        }
        while (Environment.TickCount64 - startedAt < timeoutMs);

        return NodeResult.Ok("timeout", new Dictionary<string, object?>
        {
            ["state"] = state,
            ["reason"] = "Process wait timed out"
        });
    }

    private static Process? FindProcess(string processName, string processPath)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(processName)
                    && !string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    process.Dispose();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    var actualPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(actualPath)
                        || !string.Equals(Path.GetFullPath(actualPath), Path.GetFullPath(processPath), StringComparison.OrdinalIgnoreCase))
                    {
                        process.Dispose();
                        continue;
                    }
                }

                return process;
            }
            catch
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static string NormalizeProcessName(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }
}
