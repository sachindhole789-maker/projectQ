using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.BackgroundJobs;

/// <summary>
/// Polls the notification outbox and sends emails. Run it in exactly one host
/// (the Runner); DB row locks make multiple instances safe anyway.
/// </summary>
public sealed class NotificationDispatchWorker : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOptions _options;

    public NotificationDispatchWorker(
        IOptions<NotificationOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatchWorker> logger)
        : base(logger, TimeSpan.FromSeconds(Math.Max(options.Value.PollIntervalSeconds, 5)))
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var pending = await repository.ClaimPendingAsync(
            _options.BatchSize,
            TimeSpan.FromMinutes(_options.LockMinutes),
            _options.MaxAttempts,
            cancellationToken);

        foreach (var notification in pending)
        {
            try
            {
                if (notification.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
                {
                    await emailSender.SendAsync(new EmailMessage(
                        notification.Recipient,
                        null,
                        notification.Subject,
                        notification.Body,
                        notification.Body), cancellationToken);
                }

                await repository.MarkSentAsync(notification.NotificationId, cancellationToken);
            }
            catch (Exception ex)
            {
                await repository.MarkFailedAsync(notification.NotificationId, ex.Message, cancellationToken);
            }
        }
    }
}
