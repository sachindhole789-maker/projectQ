namespace App.Application.Abstractions;

public sealed record DashboardUpdate(string EventName, object Payload, DateTime PublishedAtUtc);

/// <summary>
/// Publish any object through this interface and it is pushed to every connected
/// SignalR dashboard client. The API host owns the hub connection.
/// </summary>
public interface IRealtimeDashboardService
{
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default);

    DashboardUpdate? GetLatest();

    IAsyncEnumerable<DashboardUpdate> ReadAllAsync(CancellationToken cancellationToken = default);
}
