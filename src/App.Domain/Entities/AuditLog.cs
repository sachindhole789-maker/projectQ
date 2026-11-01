namespace App.Domain.Entities;

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int HttpStatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
