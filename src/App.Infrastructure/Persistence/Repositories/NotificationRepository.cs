using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;

namespace App.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL Server implementation of the notification outbox. The UPDATE ... OUTPUT
/// claim makes the worker safe to run on multiple instances.
/// </summary>
public sealed class NotificationRepository(IDbManager dbManager) : INotificationRepository
{
    private const string InsertSql = """
        INSERT INTO dbo.Notifications
            (NotificationId, Channel, Recipient, Subject, Body, Status, Attempts, CreatedAtUtc, SentAtUtc, LockedUntilUtc, LastError)
        VALUES
            (@NotificationId, @Channel, @Recipient, @Subject, @Body, @Status, @Attempts, @CreatedAtUtc, @SentAtUtc, @LockedUntilUtc, @LastError);
        """;

    private const string ClaimSql = """
        UPDATE TOP (@BatchSize) dbo.Notifications WITH (READPAST, UPDLOCK, ROWLOCK)
        SET Status = 'Processing',
            Attempts = Attempts + 1,
            LockedUntilUtc = @LockedUntilUtc
        OUTPUT inserted.NotificationId, inserted.Channel, inserted.Recipient, inserted.Subject,
               inserted.Body, inserted.Status, inserted.Attempts, inserted.CreatedAtUtc,
               inserted.SentAtUtc, inserted.LockedUntilUtc, inserted.LastError
        WHERE Status IN ('Pending', 'Failed')
          AND Attempts < @MaxAttempts
          AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME());
        """;

    public Task InsertAsync(QueuedNotification notification, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteTextAsync(InsertSql, notification, cancellationToken);

    public Task<IReadOnlyList<QueuedNotification>> ClaimPendingAsync(
        int batchSize,
        TimeSpan lockDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default) =>
        dbManager.QueryTextAsync<QueuedNotification>(ClaimSql, new
        {
            BatchSize = batchSize,
            LockedUntilUtc = DateTime.UtcNow.Add(lockDuration),
            MaxAttempts = maxAttempts,
        }, cancellationToken);

    public Task MarkSentAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteTextAsync(
            "UPDATE dbo.Notifications SET Status = 'Sent', SentAtUtc = SYSUTCDATETIME(), LockedUntilUtc = NULL, LastError = NULL WHERE NotificationId = @NotificationId",
            new { NotificationId = notificationId },
            cancellationToken);

    public Task MarkFailedAsync(Guid notificationId, string error, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteTextAsync(
            "UPDATE dbo.Notifications SET Status = 'Failed', LastError = @Error, LockedUntilUtc = NULL WHERE NotificationId = @NotificationId",
            new { NotificationId = notificationId, Error = error.Length > 2000 ? error[..2000] : error },
            cancellationToken);
}
