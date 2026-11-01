using App.Application.Abstractions;

namespace App.Runner;

/// <summary>
/// The runner has no SignalR hub; in-app notifications are only published by the API host.
/// </summary>
public sealed class NullRealtimePublisher : IRealtimePublisher
{
    public Task PublishNotificationAsync(
        Guid? userId,
        object notification,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishDashboardAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
