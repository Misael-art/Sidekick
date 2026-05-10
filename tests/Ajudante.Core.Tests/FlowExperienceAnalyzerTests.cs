using System.Reflection;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Tests;

public sealed class FlowExperienceAnalyzerTests
{
    [Fact]
    public void Analyze_empty_flow_returns_low_score_and_guided_creation_suggestion()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());

        var report = analyzer.Analyze(new Flow { Id = "f1", Name = "Empty" });

        Assert.True(report.Score < 70);
        Assert.Contains(report.Issues, issue => issue.Code == "flow.empty");
        Assert.Contains(report.Suggestions, suggestion => suggestion.Action == "guided.recipe");
    }

    [Fact]
    public void Analyze_unresolved_template_flags_actionable_issue()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());
        var flow = new Flow
        {
            Id = "f1",
            Name = "Template",
            Nodes =
            [
                new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
                new NodeInstance
                {
                    Id = "log",
                    TypeId = "action.logMessage",
                    Properties = new Dictionary<string, object?> { ["message"] = "Token {{missingSecret}}" }
                },
            ],
            Connections =
            [
                new Connection
                {
                    Id = "c1",
                    SourceNodeId = "start",
                    SourcePort = "triggered",
                    TargetNodeId = "log",
                    TargetPort = "in"
                }
            ]
        };

        var report = analyzer.Analyze(flow);

        Assert.False(report.CanRunWithoutAttention);
        Assert.Contains(report.Issues, issue => issue.Code == "property.template.unresolved" && issue.NodeId == "log");
        Assert.Contains(report.Suggestions, suggestion => suggestion.Action == "flow.variables");
    }

    [Fact]
    public void Analyze_weak_selector_recommends_mira_repair()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());
        var flow = new Flow
        {
            Id = "f1",
            Name = "Selector",
            Nodes =
            [
                new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
                new NodeInstance
                {
                    Id = "click",
                    TypeId = "action.desktopClickElement",
                    Properties = new Dictionary<string, object?> { ["windowTitle"] = "ERP" }
                },
            ],
            Connections =
            [
                new Connection
                {
                    Id = "c1",
                    SourceNodeId = "start",
                    SourcePort = "triggered",
                    TargetNodeId = "click",
                    TargetPort = "in"
                }
            ]
        };

        var report = analyzer.Analyze(flow);

        Assert.Contains(report.Issues, issue => issue.Code is "selector.incomplete" or "selector.weak");
        Assert.Contains(report.Suggestions, suggestion => suggestion.Action == "selector.doctor");
    }

    [Fact]
    public void Analyze_destructive_action_surfaces_safety_review()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());
        var flow = new Flow
        {
            Id = "f1",
            Name = "Delete",
            Nodes =
            [
                new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
                new NodeInstance
                {
                    Id = "delete",
                    TypeId = "action.deleteFile",
                    Properties = new Dictionary<string, object?> { ["filePath"] = "C:/temp/file.txt" }
                },
            ],
            Connections =
            [
                new Connection
                {
                    Id = "c1",
                    SourceNodeId = "start",
                    SourcePort = "triggered",
                    TargetNodeId = "delete",
                    TargetPort = "in"
                }
            ]
        };

        var report = analyzer.Analyze(flow);

        Assert.Contains(report.Issues, issue => issue.Code == "security.destructiveAction" && issue.NodeId == "delete");
        Assert.Contains(report.Suggestions, suggestion => suggestion.Action == "dryRun.review");
    }

    [Fact]
    public void Analyze_simple_valid_flow_keeps_high_score()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());
        var flow = new Flow
        {
            Id = "f1",
            Name = "Good",
            Nodes =
            [
                new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
                new NodeInstance
                {
                    Id = "log",
                    TypeId = "action.logMessage",
                    Properties = new Dictionary<string, object?> { ["message"] = "ok" }
                },
            ],
            Connections =
            [
                new Connection
                {
                    Id = "c1",
                    SourceNodeId = "start",
                    SourcePort = "triggered",
                    TargetNodeId = "log",
                    TargetPort = "in"
                }
            ]
        };

        var report = analyzer.Analyze(flow);

        Assert.True(report.Score >= 80);
        Assert.True(report.CanRunWithoutAttention);
        Assert.DoesNotContain(report.Issues, issue => issue.Severity == FlowHealthSeverity.Error);
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
            TypeId = "action.desktopClickElement",
            DisplayName = "Click Element",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "automationId", Name = "Automation Id", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "elementName", Name = "Element Name", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "controlType", Name = "Control Type", Type = PropertyType.String, DefaultValue = "" },
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
