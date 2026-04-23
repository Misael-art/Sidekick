using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class SendEmailNodeTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "AjudanteEmailTests", Guid.NewGuid().ToString("N"));

    public SendEmailNodeTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Email Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WritesEmailToPickupDirectory()
    {
        var pickupDirectory = Path.Combine(_tempRoot, "pickup");
        var attachment = Path.Combine(_tempRoot, "attachment.txt");
        await File.WriteAllTextAsync(attachment, "attachment-content");

        var node = new SendEmailNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["from"] = "sender@example.com",
            ["to"] = "receiver@example.com",
            ["subject"] = "Order {{orderId}}",
            ["body"] = "Body for {{customer}}",
            ["pickupDirectory"] = pickupDirectory,
            ["attachments"] = attachment
        });

        var context = CreateContext();
        context.SetVariable("orderId", 42);
        context.SetVariable("customer", "Alice");

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("success", result.OutputPort);
        Assert.Equal("Order 42", result.Outputs["subject"]);

        var createdFiles = Directory.GetFiles(pickupDirectory);
        Assert.NotEmpty(createdFiles);
        var messageContent = await File.ReadAllTextAsync(createdFiles[0]);
        Assert.Contains("Order 42", messageContent);
        Assert.Contains("Body for Alice", messageContent);
        Assert.Contains("receiver@example.com", messageContent);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutRecipient()
    {
        var node = new SendEmailNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["from"] = "sender@example.com",
            ["pickupDirectory"] = Path.Combine(_tempRoot, "pickup")
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("recipient", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
