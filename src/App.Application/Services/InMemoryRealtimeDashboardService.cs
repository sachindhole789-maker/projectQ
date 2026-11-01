using System.Runtime.CompilerServices;
using System.Threading.Channels;
using App.Application.Abstractions;

namespace App.Application.Services;

/// <summary>
/// Bounded fan-out buffer. Publishing never blocks the request path; old updates
/// are dropped first when a client is slower than the producer.
/// </summary>
public sealed class InMemoryRealtimeDashboardService : IRealtimeDashboardService
{
    private readonly Channel<DashboardUpdate> _channel = Channel.CreateBounded<DashboardUpdate>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false,
        });

    private DashboardUpdate? _latest;

    public async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var update = new DashboardUpdate(eventName, payload, DateTime.UtcNow);
        Volatile.Write(ref _latest, update);
        await _channel.Writer.WriteAsync(update, cancellationToken);
    }

    public DashboardUpdate? GetLatest() => Volatile.Read(ref _latest);

    public async IAsyncEnumerable<DashboardUpdate> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }
}
