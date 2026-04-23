using System.Drawing;
using System.Windows.Forms;

namespace Ajudante.Platform.Notifications;

public static class DesktopNotificationService
{
    public static async Task ShowAsync(string title, string message, int timeoutMs, CancellationToken ct)
    {
        using var notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            BalloonTipTitle = title,
            BalloonTipText = message
        };

        notifyIcon.ShowBalloonTip(Math.Max(1000, timeoutMs));

        // Keep the icon alive briefly so Windows can receive the balloon request.
        await Task.Delay(Math.Min(Math.Max(timeoutMs, 1000), 2000), ct);
    }
}
