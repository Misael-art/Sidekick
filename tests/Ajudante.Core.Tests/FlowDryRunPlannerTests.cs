using System.Reflection;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Tests;

public sealed class FlowDryRunPlannerTests
{
    [Fact]
    public void CreateReport_lists_steps_in_flow_order()
    {
        var planner = new FlowDryRunPlanner(CreateRegistry());
        var flow = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance
            {
                Id = "log",
                TypeId = "action.logMessage",
                Properties = new Dictionary<string, object?> { ["message"] = "ok" }
            },
        ]);

        var report = planner.CreateReport(flow);

        Assert.True(report.CanRun);
        Assert.Equal(["start", "log"], report.Steps.Select(step => step.NodeId).ToArray());
        Assert.All(report.Steps, step => Assert.False(step.RequiresConfirmation));
    }

    [Fact]
    public void CreateReport_marks_destructive_step_as_confirmation_required()
    {
        var planner = new FlowDryRunPlanner(CreateRegistry());
        var flow = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance
            {
                Id = "delete",
                TypeId = "action.deleteFile",
                Properties = new Dictionary<string, object?> { ["filePath"] = "C:/temp/file.txt" }
            },
        ]);

        var report = planner.CreateReport(flow);

        Assert.False(report.CanRun);
        Assert.Contains(report.Steps, step => step.NodeId == "delete" && step.RequiresConfirmation);
        Assert.Contains(report.Checkpoints, checkpoint => checkpoint.Kind == "destructive-action");
    }

    [Fact]
    public void CreateReport_blocks_missing_required_property_before_runtime()
    {
        var planner = new FlowDryRunPlanner(CreateRegistry());
        var flow = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance { Id = "delete", TypeId = "action.deleteFile" },
        ]);

        var report = planner.CreateReport(flow);

        Assert.False(report.CanRun);
        Assert.Contains(report.Validation.Errors, error => error.Contains("File Path"));
        Assert.Contains(report.Steps, step => step.NodeId == "delete" && step.Status == DryRunStepStatus.Blocked);
    }

    [Fact]
    public void CreateReport_describes_macro_steps_in_user_language()
    {
        var planner = new FlowDryRunPlanner(CreateRegistry());
        var flow = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance
            {
                Id = "wait",
                TypeId = "action.desktopWaitElement",
                Properties = new Dictionary<string, object?> { ["windowTitle"] = "ERP", ["elementName"] = "Enviar" }
            },
            new NodeInstance
            {
                Id = "click",
                TypeId = "action.desktopClickElement",
                Properties = new Dictionary<string, object?> { ["windowTitle"] = "ERP", ["elementName"] = "Enviar" }
            },
            new NodeInstance
            {
                Id = "type",
                TypeId = "action.keyboardType",
                Properties = new Dictionary<string, object?> { ["text"] = "" }
            },
        ]);

        var report = planner.CreateReport(flow);

        Assert.Contains(report.Steps, step => step.NodeId == "wait" && step.Message.Contains("aguardar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Steps, step => step.NodeId == "click" && step.Message.Contains("clicar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Steps, step => step.NodeId == "type" && step.Message.Contains("texto redigido", StringComparison.OrdinalIgnoreCase));
    }

    private static Flow CreateConnectedFlow(IReadOnlyList<NodeInstance> nodes)
    {
        var flow = new Flow
        {
            Id = "flow",
            Name = "Dry Run",
            Nodes = nodes.ToList()
        };

        for (var i = 0; i < nodes.Count - 1; i++)
        {
            flow.Connections.Add(new Connection
            {
                Id = $"c{i}",
                SourceNodeId = nodes[i].Id,
                SourcePort = i == 0 ? "triggered" : "out",
                TargetNodeId = nodes[i + 1].Id,
                TargetPort = "in"
            });
        }

        return flow;
    }

    private static TestNodeRegistry CreateRegistry()
    {
        var registry = new TestNodeRegistry();
        registry.Register(new NodeDefinition
        {
            TypeId = "trigger.manualStart",
            DisplayName = "Start Manual",
            Category = NodeCategory.Trigger,
            OutputPorts = [new PortDefinition { Id = "triggered", Name = "Triggered" }]
        });
        registry.Register(new NodeDefinition
        {
            TypeId = "action.logMessage",
            DisplayName = "Log",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "message", Name = "Message", Type = PropertyType.String, DefaultValue = "" }
            ]
        });
        registry.Register(new NodeDefinition
        {
            TypeId = "action.deleteFile",
            DisplayName = "Delete File",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "" }
            ]
        });
        registry.Register(new NodeDefinition
        {
            TypeId = "action.desktopWaitElement",
            DisplayName = "Desktop Wait Element",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "" },
            ]
        });
        registry.Register(new NodeDefinition
        {
            TypeId = "action.desktopClickElement",
            DisplayName = "Desktop Click Element",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "" },
            ]
        });
        registry.Register(new NodeDefinition
        {
            TypeId = "action.keyboardType",
            DisplayName = "Keyboard Type",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "text", Name = "Text", Type = PropertyType.String, DefaultValue = "" },
            ]
        });

        return registry;
    }

    private sealed class TestNodeRegistry : INodeRegistry
    {
        private readonly Dictionary<string, NodeDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

        public void Register(NodeDefinition definition) => _definitions[definition.TypeId] = definition;
        public void ScanAssembly(Assembly assembly) { }
        public void ScanDirectory(string pluginPath) { }
        public NodeDefinition[] GetAllDefinitions() => _definitions.Values.ToArray();
        public INode CreateInstance(string typeId) => throw new NotSupportedException();
        public NodeDefinition? GetDefinition(string typeId) => _definitions.GetValueOrDefault(typeId);
    }
}
