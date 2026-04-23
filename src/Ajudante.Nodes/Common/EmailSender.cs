using System.Net;
using System.Net.Mail;

namespace Ajudante.Nodes.Common;

internal static class EmailSender
{
    public static async Task SendAsync(
        string host,
        int port,
        bool enableSsl,
        string username,
        string password,
        string pickupDirectory,
        string from,
        IEnumerable<string> to,
        string subject,
        string body,
        IEnumerable<string> attachments,
        CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        foreach (var recipient in to.Where(value => !string.IsNullOrWhiteSpace(value)))
            message.To.Add(recipient);

        foreach (var attachmentPath in attachments.Where(File.Exists))
            message.Attachments.Add(new Attachment(attachmentPath));

        using var client = new SmtpClient();

        if (!string.IsNullOrWhiteSpace(pickupDirectory))
        {
            Directory.CreateDirectory(pickupDirectory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = pickupDirectory;
        }
        else
        {
            client.Host = host;
            client.Port = port;
            client.EnableSsl = enableSsl;
            if (!string.IsNullOrWhiteSpace(username))
                client.Credentials = new NetworkCredential(username, password);
        }

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }
}
