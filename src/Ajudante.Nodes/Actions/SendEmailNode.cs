using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.sendEmail",
    DisplayName = "Send Email",
    Category = NodeCategory.Action,
    Description = "Sends an email through SMTP or a pickup directory",
    Color = "#22C55E")]
public class SendEmailNode : IActionNode
{
    private Dictionary<string, object?> _properties = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.sendEmail",
        DisplayName = "Send Email",
        Category = NodeCategory.Action,
        Description = "Sends an email through SMTP or a pickup directory",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "success", Name = "Success", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "subject", Name = "Subject", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "from", Name = "From", Type = PropertyType.String, DefaultValue = "", Description = "Sender email address" },
            new() { Id = "to", Name = "To", Type = PropertyType.String, DefaultValue = "", Description = "Recipient list separated by semicolons" },
            new() { Id = "subject", Name = "Subject", Type = PropertyType.String, DefaultValue = "", Description = "Email subject" },
            new() { Id = "body", Name = "Body", Type = PropertyType.String, DefaultValue = "", Description = "Email body" },
            new() { Id = "host", Name = "SMTP Host", Type = PropertyType.String, DefaultValue = "", Description = "SMTP server host" },
            new() { Id = "port", Name = "SMTP Port", Type = PropertyType.Integer, DefaultValue = 25, Description = "SMTP server port" },
            new() { Id = "username", Name = "Username", Type = PropertyType.String, DefaultValue = "", Description = "Optional SMTP username" },
            new() { Id = "password", Name = "Password", Type = PropertyType.String, DefaultValue = "", Description = "Optional SMTP password" },
            new() { Id = "enableSsl", Name = "Enable SSL", Type = PropertyType.Boolean, DefaultValue = false, Description = "Enable SSL/TLS" },
            new() { Id = "pickupDirectory", Name = "Pickup Directory", Type = PropertyType.FolderPath, DefaultValue = "", Description = "Optional local pickup directory for deterministic delivery/tests" },
            new() { Id = "attachments", Name = "Attachments", Type = PropertyType.String, DefaultValue = "", Description = "Optional attachment paths separated by semicolons" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = properties;
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var from = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "from"));
        var to = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "to"));
        var subject = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "subject"));
        var body = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "body"));
        var host = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "host"));
        var port = NodeValueHelper.GetInt(_properties, "port", 25);
        var username = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "username"));
        var password = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "password"));
        var enableSsl = NodeValueHelper.GetBool(_properties, "enableSsl");
        var pickupDirectory = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "pickupDirectory"));
        var attachmentList = context.ResolveTemplate(NodeValueHelper.GetString(_properties, "attachments"))
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var recipients = to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(from))
            return NodeResult.Fail("From address is required");
        if (recipients.Length == 0)
            return NodeResult.Fail("At least one recipient is required");
        if (string.IsNullOrWhiteSpace(pickupDirectory) && string.IsNullOrWhiteSpace(host))
            return NodeResult.Fail("SMTP host or pickup directory is required");

        try
        {
            await EmailSender.SendAsync(
                host,
                port,
                enableSsl,
                username,
                password,
                pickupDirectory,
                from,
                recipients,
                subject,
                body,
                attachmentList,
                ct);

            return NodeResult.Ok("success", new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["recipientCount"] = recipients.Length
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["recipientCount"] = recipients.Length,
                ["error"] = ex.Message
            });
        }
    }
}
