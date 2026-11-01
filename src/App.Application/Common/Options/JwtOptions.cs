namespace App.Application.Common.Options;

/// <summary>
/// JWT tuning notes:
/// - Keep access tokens short (15-30 min). Refresh tokens are rotated on every use.
/// - ClockSkewSeconds is deliberately 30s (default is 5 min, far too forgiving).
/// - SigningKey may be plaintext or <c>enc:...</c>; encrypted values are decrypted
///   with Security:MasterKey at startup.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Rest.API";
    public string Audience { get; set; } = "Rest.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 20;
    public int RefreshTokenDays { get; set; } = 14;
    public int ClockSkewSeconds { get; set; } = 30;
}
