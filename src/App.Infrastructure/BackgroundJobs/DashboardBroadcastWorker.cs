using App.Application.Abstractions;
using App.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.BackgroundJobs;

public sealed record DashboardMetricsPayload(
    string Environment,
    RequestMetricsSnapshot Metrics,
    DateTime ServerTimeUtc);

/// <summary>
/// Pushes server metrics to every dashboard client. Wired in the API host where
/// the SignalR relay exists.
/// </summary>
public sealed class DashboardBroadcastWorker(
    IRealtimeDashboardService dashboard,
    IRequestMetrics requestMetrics,
    IEnvironmentInfo environment,
    TimeProvider timeProvider,
    IOptions<BackgroundJobOptions> options,
    Microsoft.Extensions.Logging.ILogger<DashboardBroadcastWorker> logger)
    : PeriodicBackgroundService(
        logger,
        TimeSpan.FromSeconds(Math.Max(options.Value.DashboardIntervalSeconds, 2)))
{
    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        var payload = new DashboardMetricsPayload(
            environment.Name,
            requestMetrics.Snapshot(),
            timeProvider.GetUtcNow().UtcDateTime);

        return dashboard.PublishAsync("dashboard.metrics", payload, cancellationToken);
    }
}
