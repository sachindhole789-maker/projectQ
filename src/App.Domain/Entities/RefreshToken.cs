namespace App.Domain.Entities;

public sealed class RefreshToken
{
    public Guid TokenId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the opaque refresh token. Only the hash is stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
}
