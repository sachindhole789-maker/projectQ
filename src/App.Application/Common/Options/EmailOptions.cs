namespace App.Application.Common.Options;

/// <summary>
/// Email transport. SMTP keeps the template provider-agnostic and free:
/// Mailtrap / Mailpit for dev, Brevo or Resend SMTP in production.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Smtp | Log</summary>
    public string Provider { get; set; } = "Log";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseStartTls { get; set; }
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "Rest API";
    public int TimeoutSeconds { get; set; } = 30;
}
