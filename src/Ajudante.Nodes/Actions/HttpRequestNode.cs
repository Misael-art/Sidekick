using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.httpRequest",
    DisplayName = "HTTP Request",
    Category = NodeCategory.Action,
    Description = "Performs an HTTP request and returns the response",
    Color = "#22C55E")]
public class HttpRequestNode : IActionNode
{
    private string _method = "GET";
    private string _url = "";
    private string _body = "";
    private string _contentType = "application/json";
    private string _headers = "";
    private string _storeBodyInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.httpRequest",
        DisplayName = "HTTP Request",
        Category = NodeCategory.Action,
        Description = "Performs an HTTP request and returns the response",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "success", Name = "Success", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "statusCode", Name = "Status Code", DataType = PortDataType.Number },
            new() { Id = "body", Name = "Body", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "method", Name = "Method", Type = PropertyType.Dropdown, DefaultValue = "GET", Description = "HTTP method", Options = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" } },
            new() { Id = "url", Name = "URL", Type = PropertyType.String, DefaultValue = "", Description = "Target URL" },
            new() { Id = "body", Name = "Body", Type = PropertyType.String, DefaultValue = "", Description = "Optional request body" },
            new() { Id = "contentType", Name = "Content Type", Type = PropertyType.String, DefaultValue = "application/json", Description = "Content type for the request body" },
            new() { Id = "headers", Name = "Headers", Type = PropertyType.String, DefaultValue = "", Description = "Optional request headers, one per line as Name: Value" },
            new() { Id = "storeBodyInVariable", Name = "Store Body In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the response body" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _method = NodeValueHelper.GetString(properties, "method", "GET");
        _url = NodeValueHelper.GetString(properties, "url");
        _body = NodeValueHelper.GetString(properties, "body");
        _contentType = NodeValueHelper.GetString(properties, "contentType", "application/json");
        _headers = NodeValueHelper.GetString(properties, "headers");
        _storeBodyInVariable = NodeValueHelper.GetString(properties, "storeBodyInVariable");
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedUrl = context.ResolveTemplate(_url);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
            return NodeResult.Fail("URL is required");

        try
        {
            var response = await HttpClientProvider.SendAsync(
                _method,
                resolvedUrl,
                context.ResolveTemplate(_body),
                context.ResolveTemplate(_contentType),
                context.ResolveTemplate(_headers),
                ct);

            NodeValueHelper.SetVariableIfRequested(context, _storeBodyInVariable, response.body);

            var outputPort = response.statusCode is >= 200 and < 300 ? "success" : "error";
            return NodeResult.Ok(outputPort, new Dictionary<string, object?>
            {
                ["statusCode"] = response.statusCode,
                ["body"] = response.body,
                ["reasonPhrase"] = response.reasonPhrase
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["statusCode"] = 0,
                ["body"] = string.Empty,
                ["reasonPhrase"] = ex.Message
            });
        }
    }
}
