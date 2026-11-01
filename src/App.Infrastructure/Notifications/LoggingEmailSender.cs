using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Notifications;

/// <summary>Writes emails to the log. Used when Email:Provider is Log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email (log transport) to {Recipient}: {Subject}{NewLine}{Body}",
            message.ToEmail,
            message.Subject,
            Environment.NewLine,
            message.HtmlBody);
        return Task.CompletedTask;
    }
}
