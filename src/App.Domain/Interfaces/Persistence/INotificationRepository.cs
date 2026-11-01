using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface INotificationRepository
{
    Task InsertAsync(QueuedNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims pending notifications for one dispatcher instance.
    /// Rows are locked for <paramref name="lockDuration"/> so multiple runner
    /// instances do not send the same notification twice.
    /// </summary>
    Task<IReadOnlyList<QueuedNotification>> ClaimPendingAsync(
        int batchSize,
        TimeSpan lockDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid notificationId, string error, CancellationToken cancellationToken = default);
}
