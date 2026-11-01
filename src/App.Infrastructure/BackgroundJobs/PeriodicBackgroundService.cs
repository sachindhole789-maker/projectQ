using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs immediately, then on a fixed interval. Exceptions are logged and do not
/// kill the worker; cancellation stops it cleanly.
/// </summary>
public abstract class PeriodicBackgroundService(ILogger logger, TimeSpan interval) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Worker} iteration failed", GetType().Name);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    protected abstract Task ExecuteOnceAsync(CancellationToken cancellationToken);
}
