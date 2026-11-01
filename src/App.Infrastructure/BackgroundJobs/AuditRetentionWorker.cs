using App.Application.Common.Options;
using App.Domain.Interfaces.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.BackgroundJobs;

public sealed class AuditRetentionWorker(
    IOptions<AuditLogOptions> auditOptions,
    IOptions<BackgroundJobOptions> jobOptions,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuditRetentionWorker> logger)
    : PeriodicBackgroundService(logger, TimeSpan.FromHours(Math.Max(jobOptions.Value.RetentionSweepHours, 1)))
{
    private readonly AuditLogOptions _auditOptions = auditOptions.Value;

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-_auditOptions.RetentionDays);
        var deleted = await repository.PurgeOlderThanAsync(cutoff, cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Purged {Count} audit rows older than {Cutoff:O}", deleted, cutoff);
        }
    }
}
