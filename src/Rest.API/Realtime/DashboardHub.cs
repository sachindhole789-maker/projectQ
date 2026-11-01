using App.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Rest.API.Realtime;

/// <summary>
/// Dashboard clients subscribe at <c>/hubs/dashboard</c>. The latest buffered
/// update is replayed on connect, then every published update is broadcast.
/// </summary>
[Authorize]
public sealed class DashboardHub(IRealtimeDashboardService dashboard) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var latest = dashboard.GetLatest();
        if (latest is not null)
        {
            await Clients.Caller.SendAsync(
                "dashboard.update",
                latest.EventName,
                latest.Payload,
                latest.PublishedAtUtc);
        }

        await base.OnConnectedAsync();
    }
}
