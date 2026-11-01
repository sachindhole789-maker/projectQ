namespace App.Application.Common.Options;

public sealed class AuditLogOptions
{
    public const string SectionName = "AuditLog";

    public bool Enabled { get; set; } = true;
    public bool CaptureRequestBody { get; set; } = true;
    public bool CaptureResponseBody { get; set; } = true;
    public int MaxBodyCharacters { get; set; } = 8_000;
    public int QueueCapacity { get; set; } = 10_000;
    public int BatchSize { get; set; } = 50;
    public int FlushSeconds { get; set; } = 2;
    public int RetentionDays { get; set; } = 90;
    public string[] ExcludedPaths { get; set; } = ["/health", "/openapi", "/scalar", "/hubs"];
    public string[] SensitiveJsonKeys { get; set; } =
    [
        "password", "newPassword", "currentPassword", "token", "refreshToken",
        "accessToken", "authorization", "secret", "clientSecret", "apiKey",
    ];
}
