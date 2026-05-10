using System.Text;
using System.Text.Json;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.listRunnableFlows",
    DisplayName = "List Runnable Flows",
    Category = NodeCategory.Action,
    Description = "Builds a safe numbered menu of flows that can be invoked by chat or concierge flows",
    Color = "#22C55E")]
public sealed class ListRunnableFlowsNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.listRunnableFlows",
        DisplayName = "List Runnable Flows",
        Category = NodeCategory.Action,
        Description = "Builds a safe numbered menu of flows that can be invoked by chat or concierge flows",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "unavailable", Name = "Unavailable", DataType = PortDataType.Flow },
            new() { Id = "menu", Name = "Menu", DataType = PortDataType.String },
            new() { Id = "catalog", Name = "Catalog", DataType = PortDataType.String },
            new() { Id = "count", Name = "Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "allowedFlowIds", Name = "Allowed Flow IDs", Type = PropertyType.String, DefaultValue = "portfolio-*", Description = "Comma or line separated allowlist. Wildcards are supported." },
            new() { Id = "includeHighRisk", Name = "Include High Risk", Type = PropertyType.Boolean, DefaultValue = false, Description = "Show high-risk flows as requiring local confirmation." },
            new() { Id = "startNumber", Name = "Start Number", Type = PropertyType.Integer, DefaultValue = 10, Description = "First menu number assigned to runnable flows." },
            new() { Id = "storeMenuInVariable", Name = "Store Menu In Variable", Type = PropertyType.String, DefaultValue = "runnableFlowsMenu", Description = "Variable that receives the rendered menu." },
            new() { Id = "storeCatalogInVariable", Name = "Store Catalog In Variable", Type = PropertyType.String, DefaultValue = "runnableFlowsCatalog", Description = "Variable that receives the number-to-flow mapping JSON." }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        if (context.FlowInvocationService == null)
        {
            const string message = "Flow invocation service is not available in this runtime.";
            return NodeResult.Ok("unavailable", new Dictionary<string, object?>
            {
                ["message"] = message,
                ["menu"] = "",
                ["catalog"] = "[]",
                ["count"] = 0
            });
        }

        var startNumber = Math.Max(1, NodeValueHelper.GetInt(_properties, "startNumber", 10));
        var summaries = await context.FlowInvocationService.ListRunnableFlowsAsync(new RunnableFlowQuery
        {
            AllowedFlowIds = ParseList(NodeValueHelper.ResolveTemplateProperty(context, _properties, "allowedFlowIds", "portfolio-*")),
            IncludeHighRisk = NodeValueHelper.GetBool(_properties, "includeHighRisk", false),
            CurrentFlowId = context.Flow.Id
        }, ct);

        var catalog = summaries
            .Select((summary, index) => new FlowMenuItem(
                startNumber + index,
                summary.FlowId,
                summary.Name,
                summary.RiskLevel,
                summary.RequiresLocalConfirmation))
            .ToArray();

        var menu = BuildMenu(catalog);
        var catalogJson = JsonSerializer.Serialize(catalog, JsonOptions);

        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeMenuInVariable", "runnableFlowsMenu"), menu);
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeCatalogInVariable", "runnableFlowsCatalog"), catalogJson);

        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["menu"] = menu,
            ["catalog"] = catalogJson,
            ["count"] = catalog.Length
        });
    }

    private static string BuildMenu(IReadOnlyList<FlowMenuItem> catalog)
    {
        if (catalog.Count == 0)
            return "Nenhum flow do portfolio esta disponivel para execucao por chat.";

        var builder = new StringBuilder();
        foreach (var item in catalog)
        {
            builder.Append(item.Number)
                .Append(" - ")
                .Append(item.Name);

            if (item.RequiresLocalConfirmation)
                builder.Append(" (requer confirmacao local)");

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string[] ParseList(string value)
    {
        return value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .DefaultIfEmpty("portfolio-*")
            .ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record FlowMenuItem(int Number, string FlowId, string Name, string RiskLevel, bool RequiresLocalConfirmation);
}

[NodeInfo(
    TypeId = "action.runSavedFlow",
    DisplayName = "Run Saved Flow",
    Category = NodeCategory.Action,
    Description = "Safely queues another saved flow through the host invocation service",
    Color = "#22C55E")]
public sealed class RunSavedFlowNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.runSavedFlow",
        DisplayName = "Run Saved Flow",
        Category = NodeCategory.Action,
        Description = "Safely queues another saved flow through the host invocation service",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "queued", Name = "Queued", DataType = PortDataType.Flow },
            new() { Id = "blocked", Name = "Blocked", DataType = PortDataType.Flow },
            new() { Id = "needsConfiguration", Name = "Needs Configuration", DataType = PortDataType.Flow },
            new() { Id = "requiresConfirmation", Name = "Requires Confirmation", DataType = PortDataType.Flow },
            new() { Id = "notFound", Name = "Not Found", DataType = PortDataType.Flow },
            new() { Id = "invalid", Name = "Invalid", DataType = PortDataType.Flow },
            new() { Id = "unavailable", Name = "Unavailable", DataType = PortDataType.Flow },
            new() { Id = "result", Name = "Result", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "flowId", Name = "Flow ID", Type = PropertyType.String, DefaultValue = "{{requestedFlowId}}", Description = "Saved flow id to queue. Supports {{variable}} templates." },
            new() { Id = "source", Name = "Source", Type = PropertyType.String, DefaultValue = "whatsapp", Description = "Source label used in audit logs." },
            new() { Id = "requestedBy", Name = "Requested By", Type = PropertyType.String, DefaultValue = "{{whatsappOwnerPhone}}", Description = "Requester identity, for example the normalized WhatsApp number." },
            new() { Id = "allowedFlowIds", Name = "Allowed Flow IDs", Type = PropertyType.String, DefaultValue = "portfolio-*", Description = "Comma or line separated allowlist. Wildcards are supported." },
            new() { Id = "allowHighRisk", Name = "Allow High Risk", Type = PropertyType.Boolean, DefaultValue = false, Description = "High risk flows still require local confirmation unless this is set by a trusted local action." },
            new() { Id = "storeResultInVariable", Name = "Store Result In Variable", Type = PropertyType.String, DefaultValue = "runFlowResult", Description = "Variable that receives a user-readable status." }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        if (context.FlowInvocationService == null)
        {
            return BuildResult(context, "unavailable", new FlowInvocationResult
            {
                Status = FlowInvocationStatus.Unavailable,
                Message = "Flow invocation service is not available in this runtime."
            });
        }

        var flowId = NodeValueHelper.ResolveTemplateProperty(context, _properties, "flowId", "");
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return BuildResult(context, "notFound", new FlowInvocationResult
            {
                Status = FlowInvocationStatus.NotFound,
                Message = "Nenhum flow foi informado."
            });
        }

        var result = await context.FlowInvocationService.QueueFlowAsync(new FlowInvocationRequest
        {
            FlowId = flowId.Trim(),
            Source = NodeValueHelper.ResolveTemplateProperty(context, _properties, "source", "whatsapp"),
            RequestedBy = NodeValueHelper.ResolveTemplateProperty(context, _properties, "requestedBy", ""),
            AllowHighRisk = NodeValueHelper.GetBool(_properties, "allowHighRisk", false),
            CurrentFlowId = context.Flow.Id,
            AllowedFlowIds = ParseList(NodeValueHelper.ResolveTemplateProperty(context, _properties, "allowedFlowIds", "portfolio-*"))
        }, ct);

        return BuildResult(context, StatusToPort(result.Status), result);
    }

    private NodeResult BuildResult(FlowExecutionContext context, string outputPort, FlowInvocationResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.Message)
            ? result.Status.ToString()
            : result.Message;
        var resultJson = JsonSerializer.Serialize(new
        {
            status = outputPort,
            flowId = result.FlowId,
            flowName = result.FlowName,
            message
        }, JsonOptions);

        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeResultInVariable", "runFlowResult"), message);

        return NodeResult.Ok(outputPort, new Dictionary<string, object?>
        {
            ["status"] = outputPort,
            ["flowId"] = result.FlowId,
            ["flowName"] = result.FlowName,
            ["message"] = message,
            ["result"] = resultJson
        });
    }

    private static string StatusToPort(FlowInvocationStatus status)
    {
        return status switch
        {
            FlowInvocationStatus.Queued => "queued",
            FlowInvocationStatus.Blocked => "blocked",
            FlowInvocationStatus.NeedsConfiguration => "needsConfiguration",
            FlowInvocationStatus.RequiresLocalConfirmation => "requiresConfirmation",
            FlowInvocationStatus.NotFound => "notFound",
            FlowInvocationStatus.Invalid => "invalid",
            _ => "unavailable"
        };
    }

    private static string[] ParseList(string value)
    {
        return value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .DefaultIfEmpty("portfolio-*")
            .ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
