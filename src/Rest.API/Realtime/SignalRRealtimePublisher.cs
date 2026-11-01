using App.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Rest.API.Realtime;

public sealed class SignalRRealtimePublisher(
    IHubContext<DashboardHub> dashboardHub,
    IHubContext<NotificationHub> notificationHub) : IRealtimePublisher
{
    public Task PublishNotificationAsync(
        Guid? userId,
        object notification,
        CancellationToken cancellationToken = default)
    {
        var clients = userId is null
            ? notificationHub.Clients.All
            : notificationHub.Clients.User(userId.Value.ToString());

        return clients.SendAsync("notification.received", notification, cancellationToken);
    }

    public Task PublishDashboardAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) =>
        dashboardHub.Clients.All.SendAsync("dashboard.update", eventName, payload, cancellationToken);
}
