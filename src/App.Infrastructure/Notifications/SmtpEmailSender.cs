using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace App.Infrastructure.Notifications;

/// <summary>
/// Provider-agnostic SMTP transport (Mailtrap/Mailpit in dev, Brevo/Resend in
/// production). TLS is required unless the host is localhost.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ISecretResolver secretResolver,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToEmail));
        mime.Subject = message.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody ?? message.HtmlBody,
        };

        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = _options.TimeoutSeconds * 1000,
        };

        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            var password = secretResolver.Resolve(_options.Password ?? string.Empty);
            await client.AuthenticateAsync(_options.Username, password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Email sent to {Recipient} with subject {Subject}", MaskEmail(message.ToEmail), message.Subject);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[(at - 1)..]}";
    }
}
