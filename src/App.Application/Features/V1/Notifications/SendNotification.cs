using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Services;
using App.Domain.Interfaces.Infrastructure;
using FluentValidation;

namespace App.Application.Features.V1.Notifications;

public sealed record SendNotificationCommand(
    NotificationChannel Channel,
    string Recipient,
    string Subject,
    string Body) : ICommand<SendNotificationResponse>;

public sealed record SendNotificationResponse(Guid NotificationId);

public sealed class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Recipient).EmailAddress()
            .When(x => x.Channel == NotificationChannel.Email)
            .WithMessage("Recipient must be a valid email address.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}

public sealed class SendNotificationCommandHandler(
    INotificationService notificationService,
    IUserInfoProvider userInfo) : ICommandHandler<SendNotificationCommand, SendNotificationResponse>
{
    public async Task<Result<SendNotificationResponse>> Handle(
        SendNotificationCommand command,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<SendNotificationResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var id = await notificationService.EnqueueAsync(new NotificationRequest(
            command.Channel,
            command.Recipient,
            command.Subject,
            command.Body,
            userInfo.UserId), cancellationToken);

        return Result<SendNotificationResponse>.Success(
            new SendNotificationResponse(id),
            command.Channel == NotificationChannel.Email
                ? "Notification queued"
                : "Notification sent");
    }
}
