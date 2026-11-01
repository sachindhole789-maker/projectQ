namespace App.Application.Abstractions;

/// <summary>In-app notifications over SignalR, implemented in the API host.</summary>
public interface IRealtimePublisher
{
    Task PublishNotificationAsync(Guid? userId, object notification, CancellationToken cancellationToken = default);

    Task PublishDashboardAsync(string eventName, object payload, CancellationToken cancellationToken = default);
}
