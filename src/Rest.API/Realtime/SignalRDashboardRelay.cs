using App.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Rest.API.Realtime;

/// <summary>
/// Bridges the Application dashboard buffer to SignalR. Any handler that calls
/// <see cref="IRealtimeDashboardService.PublishAsync"/> reaches every connected client.
/// </summary>
public sealed class SignalRDashboardRelay(
    IRealtimeDashboardService dashboard,
    IHubContext<DashboardHub> hub,
    ILogger<SignalRDashboardRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var update in dashboard.ReadAllAsync(stoppingToken))
            {
                await hub.Clients.All.SendAsync(
                    "dashboard.update",
                    update.EventName,
                    update.Payload,
                    update.PublishedAtUtc,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard SignalR relay stopped unexpectedly");
            throw;
        }
    }
}
