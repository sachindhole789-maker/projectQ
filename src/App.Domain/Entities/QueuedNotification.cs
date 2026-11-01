namespace App.Domain.Entities;

public sealed class QueuedNotification
{
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Sent | Failed</summary>
    public string Status { get; set; } = "Pending";

    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? LastError { get; set; }
}
