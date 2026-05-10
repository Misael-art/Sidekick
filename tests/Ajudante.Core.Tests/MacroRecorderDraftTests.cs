using System.Reflection;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Recorder;

namespace Ajudante.Core.Tests;

public sealed class MacroRecorderDraftTests
{
    [Fact]
    public void Coalesce_groups_printable_key_presses_into_text_input()
    {
        var startedAt = DateTime.UtcNow;
        var events = new[]
        {
            Key("h", startedAt),
            Key("e", startedAt.AddMilliseconds(20)),
            Key("l", startedAt.AddMilliseconds(40)),
            Key("l", startedAt.AddMilliseconds(60)),
            Key("o", startedAt.AddMilliseconds(80)),
        };

        var coalesced = MacroRecorderEventCoalescer.Coalesce(events, new MacroRecorderOptions());

        var textInput = Assert.Single(coalesced);
        Assert.Equal("textInput", textInput.Kind);
        Assert.Equal("hello", textInput.Text?.Value);
        Assert.False(textInput.Privacy.IsRedacted);
    }

    [Fact]
    public void Coalesce_redacts_probable_sensitive_text_by_default()
    {
        var startedAt = DateTime.UtcNow;
        var element = new RecorderElementContext
        {
            Name = "API Key",
            AutomationId = "apiKey",
            ControlType = "Edit"
        };
        var events = new[]
        {
            Key("s", startedAt, element),
            Key("k", startedAt.AddMilliseconds(20), element),
            Key("-", startedAt.AddMilliseconds(40), element),
            Key("p", startedAt.AddMilliseconds(60), element),
            Key("r", startedAt.AddMilliseconds(80), element),
            Key("o", startedAt.AddMilliseconds(100), element),
            Key("j", startedAt.AddMilliseconds(120), element),
            Key("-", startedAt.AddMilliseconds(140), element),
            Key("1", startedAt.AddMilliseconds(160), element),
            Key("2", startedAt.AddMilliseconds(180), element),
            Key("3", startedAt.AddMilliseconds(200), element),
        };

        var coalesced = MacroRecorderEventCoalescer.Coalesce(events, new MacroRecorderOptions());

        var redacted = Assert.Single(coalesced);
        Assert.Equal("redactedInput", redacted.Kind);
        Assert.True(redacted.Privacy.IsRedacted);
        Assert.Null(redacted.Text?.Value);
        Assert.Equal(11, redacted.Text?.Length);
    }

    [Fact]
    public void Coalesce_groups_pre_redacted_key_events_without_recovering_raw_text()
    {
        var startedAt = DateTime.UtcNow;
        var events = new[]
        {
            RedactedKey(startedAt),
            RedactedKey(startedAt.AddMilliseconds(30)),
            RedactedKey(startedAt.AddMilliseconds(60)),
        };

        var coalesced = MacroRecorderEventCoalescer.Coalesce(events, new MacroRecorderOptions());

        var redacted = Assert.Single(coalesced);
        Assert.Equal("redactedInput", redacted.Kind);
        Assert.True(redacted.Privacy.IsRedacted);
        Assert.Null(redacted.Text?.Value);
        Assert.Equal(3, redacted.Text?.Length);
    }

    [Fact]
    public void Coalesce_groups_nearby_clicks_into_double_click()
    {
        var startedAt = DateTime.UtcNow;
        var events = new[]
        {
            Click(120, 240, startedAt),
            Click(122, 241, startedAt.AddMilliseconds(210)),
        };

        var coalesced = MacroRecorderEventCoalescer.Coalesce(events, new MacroRecorderOptions());

        var click = Assert.Single(coalesced);
        Assert.Equal("mouseDoubleClick", click.Kind);
        Assert.Equal(121, click.Mouse?.X);
        Assert.Equal(240, click.Mouse?.Y);
    }

    [Fact]
    public void Coalesce_detects_drag_from_down_move_up()
    {
        var startedAt = DateTime.UtcNow;
        var events = new[]
        {
            Mouse("mouseDown", 10, 20, startedAt),
            Mouse("mouseMove", 60, 70, startedAt.AddMilliseconds(120)),
            Mouse("mouseUp", 100, 120, startedAt.AddMilliseconds(240)),
        };

        var coalesced = MacroRecorderEventCoalescer.Coalesce(events, new MacroRecorderOptions());

        var drag = Assert.Single(coalesced);
        Assert.Equal("mouseDrag", drag.Kind);
        Assert.Equal(10, drag.Mouse?.StartX);
        Assert.Equal(20, drag.Mouse?.StartY);
        Assert.Equal(100, drag.Mouse?.EndX);
        Assert.Equal(120, drag.Mouse?.EndY);
    }

    [Fact]
    public void DraftBuilder_maps_strong_uia_click_to_desktop_click_element()
    {
        var session = new MacroRecordingSession
        {
            SessionId = "session-1",
            StartedAt = DateTime.UtcNow,
            Status = "stopped",
            PrivacyMode = "redactSensitive"
        };
        var element = new RecorderElementContext
        {
            AutomationId = "submit",
            Name = "Enviar",
            ControlType = "button",
            ProcessName = "erp",
            ProcessPath = "C:/ERP/erp.exe",
            WindowTitle = "ERP - Pedido",
            Bounds = new RecorderBounds { X = 50, Y = 80, Width = 90, Height = 30 },
            RelativeX = 45,
            RelativeY = 15,
            NormalizedX = 0.2,
            NormalizedY = 0.3
        };

        var draft = MacroDraftBuilder.BuildDraft(
            session,
            [Click(95, 95, session.StartedAt.AddSeconds(1), element)],
            new MacroRecorderOptions());

        var node = Assert.Single(draft.SuggestedNodes.Where(node => node.TypeId == "action.desktopClickElement"));
        Assert.Equal("submit", node.Properties["automationId"]);
        Assert.Equal("ERP - Pedido", node.Properties["windowTitle"]);
        Assert.Equal(0, draft.Warnings.Count(warning => warning.Contains("coordenada absoluta", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void DraftBuilder_falls_back_to_absolute_click_with_warning_when_selector_is_missing()
    {
        var session = new MacroRecordingSession
        {
            SessionId = "session-2",
            StartedAt = DateTime.UtcNow,
            Status = "stopped",
            PrivacyMode = "redactSensitive"
        };

        var draft = MacroDraftBuilder.BuildDraft(
            session,
            [Click(320, 180, session.StartedAt.AddSeconds(1))],
            new MacroRecorderOptions());

        var node = Assert.Single(draft.SuggestedNodes.Where(node => node.TypeId == "action.mouseClick"));
        Assert.Equal(320, node.Properties["x"]);
        Assert.Equal(180, node.Properties["y"]);
        Assert.Contains(draft.Warnings, warning => warning.Contains("coordenada absoluta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FlowHealth_penalizes_fragile_macro_more_than_resilient_macro()
    {
        var analyzer = new FlowExperienceAnalyzer(CreateRegistry());

        var fragile = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance { Id = "click-1", TypeId = "action.mouseClick", Properties = new Dictionary<string, object?> { ["x"] = 100, ["y"] = 120 } },
            new NodeInstance { Id = "click-2", TypeId = "action.mouseClick", Properties = new Dictionary<string, object?> { ["x"] = 105, ["y"] = 121 } },
            new NodeInstance { Id = "click-3", TypeId = "action.mouseClick", Properties = new Dictionary<string, object?> { ["x"] = 106, ["y"] = 123 } },
        ]);
        var resilient = CreateConnectedFlow([
            new NodeInstance { Id = "start", TypeId = "trigger.manualStart" },
            new NodeInstance
            {
                Id = "click",
                TypeId = "action.desktopClickElement",
                Properties = new Dictionary<string, object?>
                {
                    ["automationId"] = "submit",
                    ["windowTitle"] = "ERP",
                    ["processName"] = "erp"
                }
            },
        ]);

        var fragileReport = analyzer.Analyze(fragile);
        var resilientReport = analyzer.Analyze(resilient);

        Assert.True(fragileReport.Score < resilientReport.Score);
        Assert.Contains(fragileReport.Issues, issue => issue.Code == "macro.absoluteCoordinates");
        Assert.DoesNotContain(resilientReport.Issues, issue => issue.Code == "macro.absoluteCoordinates");
    }

    private static RecorderEvent Key(string text, DateTime timestamp, RecorderElementContext? element = null)
    {
        return new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "keyPress",
            Timestamp = timestamp,
            Window = Window(),
            Element = element,
            Keyboard = new RecorderKeyboardPayload { Key = text, Text = text }
        };
    }

    private static RecorderEvent Click(int x, int y, DateTime timestamp, RecorderElementContext? element = null)
    {
        return new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "mouseClick",
            Timestamp = timestamp,
            Window = Window(),
            Element = element,
            Mouse = new RecorderMousePayload { X = x, Y = y, Button = "left" }
        };
    }

    private static RecorderEvent RedactedKey(DateTime timestamp)
    {
        return new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "redactedInput",
            Timestamp = timestamp,
            Window = Window(),
            Text = new RecorderTextPayload { Value = null, Length = 1, IsRedacted = true },
            Privacy = new RecorderPrivacyInfo
            {
                IsRedacted = true,
                Mode = "redactSensitive",
                Reason = "Campo sensivel"
            }
        };
    }

    private static RecorderEvent Mouse(string kind, int x, int y, DateTime timestamp)
    {
        return new RecorderEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Timestamp = timestamp,
            Window = Window(),
            Mouse = new RecorderMousePayload { X = x, Y = y, Button = "left" }
        };
    }

    private static RecorderWindowContext Window()
    {
        return new RecorderWindowContext
        {
            WindowTitle = "ERP",
            ProcessName = "erp",
            ProcessPath = "C:/ERP/erp.exe"
        };
    }

    private static Flow CreateConnectedFlow(IReadOnlyList<NodeInstance> nodes)
    {
        var flow = new Flow
        {
            Id = "flow",
            Name = "Macro Health",
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
            TypeId = "action.mouseClick",
            DisplayName = "Mouse Click",
            Category = NodeCategory.Action,
            InputPorts = [new PortDefinition { Id = "in", Name = "In" }],
            OutputPorts = [new PortDefinition { Id = "out", Name = "Out" }],
            Properties =
            [
                new PropertyDefinition { Id = "x", Name = "X", Type = PropertyType.Integer, DefaultValue = 0 },
                new PropertyDefinition { Id = "y", Name = "Y", Type = PropertyType.Integer, DefaultValue = 0 },
                new PropertyDefinition { Id = "button", Name = "Button", Type = PropertyType.Dropdown, DefaultValue = "left", Options = ["left", "right", "middle"] },
                new PropertyDefinition { Id = "clickType", Name = "Click Type", Type = PropertyType.Dropdown, DefaultValue = "single", Options = ["single", "double"] },
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
                new PropertyDefinition { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "" },
                new PropertyDefinition { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "" },
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
