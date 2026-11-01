namespace App.Domain.Interfaces.Infrastructure;

public sealed record VirusScanResult(bool IsClean, string? ThreatName = null)
{
    public static VirusScanResult Clean => new(true);

    public static VirusScanResult Infected(string threatName) => new(false, threatName);
}

/// <summary>
/// Malware scanning hook. A no-op implementation is used by default; point
/// VirusScan:Provider at ClamAV to enable real scanning.
/// </summary>
public interface IVirusScanner
{
    Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
