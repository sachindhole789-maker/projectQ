namespace App.Application.Common.Options;

/// <summary>
/// Tune these numbers with real traffic data:
/// - Global: per-IP burst guard for anonymous traffic.
/// - User: per authenticated user, sliding window (fairer than fixed).
/// - Auth: very strict fixed window on login/refresh to blunt credential stuffing.
/// QueueLimit > 0 makes excess requests wait instead of failing immediately.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public int GlobalPermitLimit { get; set; } = 100;
    public int GlobalWindowSeconds { get; set; } = 60;
    public int GlobalQueueLimit { get; set; } = 0;
    public int UserPermitLimit { get; set; } = 300;
    public int UserWindowSeconds { get; set; } = 60;
    public int AuthPermitLimit { get; set; } = 8;
    public int AuthWindowSeconds { get; set; } = 60;
}
