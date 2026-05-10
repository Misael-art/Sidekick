using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class FlowInvocationNodesTests
{
    [Fact]
    public async Task ListRunnableFlowsNode_BuildsNumberedMenuAndCatalog()
    {
        var service = new FakeFlowInvocationService(new[]
        {
            new RunnableFlowSummary
            {
                FlowId = "portfolio-print",
                Name = "Enviar print",
                Category = "portfolio",
                RiskLevel = "low",
                IsPortfolio = true,
                RequiresLocalConfirmation = false
            },
            new RunnableFlowSummary
            {
                FlowId = "portfolio-cleanup",
                Name = "Limpeza perigosa",
                Category = "portfolio",
                RiskLevel = "high",
                IsPortfolio = true,
                RequiresLocalConfirmation = true
            }
        });
        var context = CreateContext(service);
        var node = new ListRunnableFlowsNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["allowedFlowIds"] = "portfolio-*",
            ["startNumber"] = 10,
            ["storeMenuInVariable"] = "menu",
            ["storeCatalogInVariable"] = "catalog"
        });

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("out", result.OutputPort);
        Assert.Contains("10 - Enviar print", context.GetVariable<string>("menu"));
        Assert.Contains("11 - Limpeza perigosa", context.GetVariable<string>("menu"));
        Assert.Contains("requer confirmacao local", context.GetVariable<string>("menu"));
        Assert.Contains("portfolio-print", context.GetVariable<string>("catalog"));
        Assert.Equal("portfolio-*", service.LastQuery?.AllowedFlowIds.Single());
    }

    [Theory]
    [InlineData("1", "screenshot")]
    [InlineData("2 ipconfig", "command")]
    [InlineData("3", "systemInfo")]
    [InlineData("4", "listFlows")]
    [InlineData("10", "runFlow")]
    [InlineData("0", "stop")]
    [InlineData("2 format c:", "unknown")]
    [InlineData("algo", "unknown")]
    public async Task ChatMenuRouterNode_RoutesSupportedWhatsAppCommands(string message, string expectedPort)
    {
        var context = CreateContext();
        var node = new ChatMenuRouterNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["message"] = message,
            ["allowedCommands"] = "ipconfig,whoami,hostname",
            ["flowCatalogJson"] = """
                [{"number":10,"flowId":"portfolio-print","name":"Enviar print"}]
                """
        });

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expectedPort, result.OutputPort);
        if (expectedPort == "runFlow")
            Assert.Equal("portfolio-print", context.GetVariable<string>("requestedFlowId"));
        if (expectedPort == "command")
            Assert.Equal("ipconfig", context.GetVariable<string>("requestedCommand"));
    }

    [Fact]
    public async Task RunSavedFlowNode_QueuesThroughInvocationServiceWithoutExecutingInline()
    {
        var service = new FakeFlowInvocationService(Array.Empty<RunnableFlowSummary>())
        {
            QueueResult = new FlowInvocationResult
            {
                Status = FlowInvocationStatus.Queued,
                FlowId = "portfolio-print",
                FlowName = "Enviar print",
                Message = "enfileirado"
            }
        };
        var context = CreateContext(service, currentFlowId: "whatsapp-assistant");
        var node = new RunSavedFlowNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["flowId"] = "portfolio-print",
            ["source"] = "whatsapp",
            ["requestedBy"] = "5511959766061",
            ["storeResultInVariable"] = "runFlowResult"
        });

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("queued", result.OutputPort);
        Assert.NotNull(service.LastRequest);
        Assert.Equal("portfolio-print", service.LastRequest!.FlowId);
        Assert.Equal("whatsapp-assistant", service.LastRequest.CurrentFlowId);
        Assert.Contains("enfileirado", context.GetVariable<string>("runFlowResult"));
    }

    private static FlowExecutionContext CreateContext(
        IFlowInvocationService? service = null,
        string currentFlowId = "test-flow")
    {
        return new FlowExecutionContext(new Flow { Id = currentFlowId, Name = "Test" }, CancellationToken.None)
        {
            FlowInvocationService = service
        };
    }

    private sealed class FakeFlowInvocationService : IFlowInvocationService
    {
        private readonly IReadOnlyList<RunnableFlowSummary> _summaries;

        public FakeFlowInvocationService(IReadOnlyList<RunnableFlowSummary> summaries)
        {
            _summaries = summaries;
        }

        public RunnableFlowQuery? LastQuery { get; private set; }
        public FlowInvocationRequest? LastRequest { get; private set; }
        public FlowInvocationResult QueueResult { get; set; } = new()
        {
            Status = FlowInvocationStatus.Queued,
            FlowId = "flow",
            FlowName = "Flow",
            Message = "enfileirado"
        };

        public Task<IReadOnlyList<RunnableFlowSummary>> ListRunnableFlowsAsync(RunnableFlowQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_summaries);
        }

        public Task<FlowInvocationResult> QueueFlowAsync(FlowInvocationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(QueueResult);
        }
    }
}
