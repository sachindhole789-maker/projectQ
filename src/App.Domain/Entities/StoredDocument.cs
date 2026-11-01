namespace App.Domain.Entities;

public sealed class StoredDocument
{
    public Guid FileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Content type detected from magic bytes, not from the client header.</summary>
    public string DetectedContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public Guid? UploadedByUserId { get; set; }
    public string Environment { get; set; } = string.Empty;

    /// <summary>Pending | Clean | Infected | Failed</summary>
    public string VirusScanStatus { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; }
}
