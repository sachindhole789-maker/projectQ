using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.VirusScanning;

/// <summary>Development fallback. Never reports a file as infected.</summary>
public sealed class NoOpVirusScanner(ILogger<NoOpVirusScanner> logger) : IVirusScanner
{
    public Task<VirusScanResult> ScanAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Virus scanning is disabled; accepting {FileName}", fileName);
        return Task.FromResult(VirusScanResult.Clean);
    }
}
