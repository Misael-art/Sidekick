using Ajudante.App.Runtime;
using Ajudante.Core;
using Ajudante.Core.Registry;
using Ajudante.Core.Serialization;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class FlowInvocationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SidekickInvocationTests", Guid.NewGuid().ToString("n"));
    private readonly string _flowsDirectory;
    private readonly string _seedDirectory;

    public FlowInvocationServiceTests()
    {
        _flowsDirectory = Path.Combine(_root, "flows");
        _seedDirectory = Path.Combine(_root, "seed-flows");
        Directory.CreateDirectory(_flowsDirectory);
        Directory.CreateDirectory(_seedDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ListRunnableFlowsAsync_ListsOnlyAllowedValidPortfolioFlows()
    {
        await SaveFlowAsync(_flowsDirectory, CreateLogFlow("portfolio-safe", "Portfolio Safe"));
        await SaveFlowAsync(_flowsDirectory, CreateLogFlow("user-flow", "User Flow"));
        await SaveFlowAsync(_flowsDirectory, CreateHighRiskFlow("portfolio-danger", "Portfolio Danger"));

        using var runtime = new FlowRuntimeManager(CreateRegistry());
        var service = new FlowInvocationService(CreateRegistry(), runtime, _flowsDirectory, _seedDirectory);

        var flows = await service.ListRunnableFlowsAsync(new RunnableFlowQuery
        {
            AllowedFlowIds = ["portfolio-*"],
            IncludeHighRisk = false
        });

        var summary = Assert.Single(flows);
        Assert.Equal("portfolio-safe", summary.FlowId);
        Assert.False(summary.RequiresLocalConfirmation);
    }

    [Fact]
    public async Task QueueFlowAsync_BlocksSelfInvalidHighRiskAndOutsideAllowlist()
    {
        await SaveFlowAsync(_flowsDirectory, CreateLogFlow("portfolio-safe", "Portfolio Safe"));
        await SaveFlowAsync(_flowsDirectory, CreateInvalidFlow("portfolio-invalid", "Portfolio Invalid"));
        await SaveFlowAsync(_flowsDirectory, CreateHighRiskFlow("portfolio-danger", "Portfolio Danger"));

        using var runtime = new FlowRuntimeManager(CreateRegistry());
        var service = new FlowInvocationService(CreateRegistry(), runtime, _flowsDirectory, _seedDirectory);

        var self = await service.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = "portfolio-safe",
            CurrentFlowId = "portfolio-safe",
            AllowedFlowIds = ["portfolio-*"]
        });
        Assert.Equal(FlowInvocationStatus.Blocked, self.Status);

        var invalid = await service.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = "portfolio-invalid",
            AllowedFlowIds = ["portfolio-*"]
        });
        Assert.Equal(FlowInvocationStatus.NeedsConfiguration, invalid.Status);

        var highRisk = await service.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = "portfolio-danger",
            AllowedFlowIds = ["portfolio-*"]
        });
        Assert.Equal(FlowInvocationStatus.RequiresLocalConfirmation, highRisk.Status);

        var outsideAllowlist = await service.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = "portfolio-safe",
            AllowedFlowIds = ["approved-only"]
        });
        Assert.Equal(FlowInvocationStatus.Blocked, outsideAllowlist.Status);
    }

    [Fact]
    public async Task QueueFlowAsync_QueuesValidAllowedFlow()
    {
        await SaveFlowAsync(_flowsDirectory, CreateLogFlow("portfolio-safe", "Portfolio Safe"));

        using var runtime = new FlowRuntimeManager(CreateRegistry());
        var service = new FlowInvocationService(CreateRegistry(), runtime, _flowsDirectory, _seedDirectory);

        var result = await service.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = "portfolio-safe",
            Source = "whatsapp",
            RequestedBy = "5511959766061",
            AllowedFlowIds = ["portfolio-*"]
        });

        Assert.Equal(FlowInvocationStatus.Queued, result.Status);
        Assert.Equal("portfolio-safe", result.FlowId);
    }

    private static NodeRegistry CreateRegistry()
    {
        var registry = new NodeRegistry();
        registry.ScanAssembly(typeof(LogMessageNode).Assembly);
        return registry;
    }

    private static async Task SaveFlowAsync(string directory, Flow flow)
    {
        await FlowSerializer.SaveAsync(flow, Path.Combine(directory, $"{flow.Id}.json"));
    }

    private static Flow CreateLogFlow(string id, string name)
    {
        return new Flow
        {
            Id = id,
            Name = name,
            Nodes =
            [
                new() { Id = "start", TypeId = "trigger.manualStart" },
                new()
                {
                    Id = "log",
                    TypeId = "action.logMessage",
                    Properties = new Dictionary<string, object?> { ["message"] = "ok" }
                }
            ],
            Connections =
            [
                new() { Id = "c1", SourceNodeId = "start", SourcePort = "triggered", TargetNodeId = "log", TargetPort = "in" }
            ]
        };
    }

    private static Flow CreateInvalidFlow(string id, string name)
    {
        return new Flow
        {
            Id = id,
            Name = name,
            Nodes =
            [
                new() { Id = "start", TypeId = "trigger.manualStart" },
                new() { Id = "write", TypeId = "action.writeFile" }
            ],
            Connections =
            [
                new() { Id = "c1", SourceNodeId = "start", SourcePort = "triggered", TargetNodeId = "write", TargetPort = "in" }
            ]
        };
    }

    private static Flow CreateHighRiskFlow(string id, string name)
    {
        return new Flow
        {
            Id = id,
            Name = name,
            Nodes =
            [
                new() { Id = "start", TypeId = "trigger.manualStart" },
                new()
                {
                    Id = "delete",
                    TypeId = "action.deleteFile",
                    Properties = new Dictionary<string, object?> { ["filePath"] = "C:\\Temp\\sidekick-test.txt" }
                }
            ],
            Connections =
            [
                new() { Id = "c1", SourceNodeId = "start", SourcePort = "triggered", TargetNodeId = "delete", TargetPort = "in" }
            ]
        };
    }
}
