namespace App.Domain.Dtos.Files;

public sealed record FileDownloadInfo(string Url, DateTime? ExpiresAtUtc);
