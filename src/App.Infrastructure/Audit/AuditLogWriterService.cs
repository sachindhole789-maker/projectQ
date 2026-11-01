using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Audit;

/// <summary>
/// Drains the bounded audit channel and writes in small batches. A database
/// outage drops the current batch after logging; it never blocks requests.
/// </summary>
public sealed class AuditLogWriterService(
    IAuditLogQueue queue,
    IOptions<AuditLogOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogWriterService> logger) : BackgroundService
{
    private readonly AuditLogOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            List<AuditLog> batch;
            try
            {
                var first = await queue.Reader.ReadAsync(stoppingToken);
                batch = new List<AuditLog>(_options.BatchSize) { first };

                // Small linger window lets concurrent requests join the batch.
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.FlushSeconds, 0)), stoppingToken);

                while (batch.Count < _options.BatchSize && queue.Reader.TryRead(out var next))
                {
                    batch.Add(next);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                await repository.WriteBatchAsync(batch, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist {Count} audit log rows", batch.Count);
            }
        }
    }
}
