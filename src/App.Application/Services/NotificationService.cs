using App.Application.Abstractions;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;
using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public enum NotificationChannel
{
    Email = 0,
    InApp = 1,
}

public sealed record NotificationRequest(
    NotificationChannel Channel,
    string Recipient,
    string Subject,
    string Body,
    Guid? UserId = null);

public interface INotificationService
{
    Task<Guid> EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

public sealed class NotificationService(
    INotificationRepository repository,
    IRealtimePublisher realtimePublisher,
    TimeProvider timeProvider,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<Guid> EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var notification = new QueuedNotification
        {
            NotificationId = Guid.NewGuid(),
            Channel = request.Channel.ToString(),
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
            Status = "Pending",
            CreatedAtUtc = now,
        };

        if (request.Channel == NotificationChannel.InApp)
        {
            await realtimePublisher.PublishNotificationAsync(request.UserId, new
            {
                notification.NotificationId,
                notification.Subject,
                notification.Body,
                notification.CreatedAtUtc,
            }, cancellationToken);

            notification.Status = "Sent";
            notification.SentAtUtc = now;
            logger.LogInformation("Sent in-app notification {NotificationId}", notification.NotificationId);
        }

        await repository.InsertAsync(notification, cancellationToken);
        return notification.NotificationId;
    }
}
