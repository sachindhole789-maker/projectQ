using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace App.Application.Features.V1.Dashboard;

public sealed record PublishDashboardDataCommand(
    string EventName,
    IReadOnlyDictionary<string, object?> Payload) : ICommand<PublishDashboardDataResponse>;

public sealed record PublishDashboardDataResponse(string EventName, DateTime PublishedAtUtc);

public sealed class PublishDashboardDataCommandValidator : AbstractValidator<PublishDashboardDataCommand>
{
    public PublishDashboardDataCommandValidator()
    {
        RuleFor(x => x.EventName).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Payload).NotNull();
    }
}

/// <summary>
/// Call this from any handler/service to push live data to every connected
/// frontend. The API host relays the update through SignalR.
/// </summary>
public sealed class PublishDashboardDataCommandHandler(
    IRealtimeDashboardService dashboard,
    IUserInfoProvider userInfo,
    TimeProvider timeProvider,
    ILogger<PublishDashboardDataCommandHandler> logger)
    : ICommandHandler<PublishDashboardDataCommand, PublishDashboardDataResponse>
{
    public async Task<Result<PublishDashboardDataResponse>> Handle(
        PublishDashboardDataCommand command,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<PublishDashboardDataResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        await dashboard.PublishAsync(command.EventName, command.Payload, cancellationToken);
        var publishedAt = timeProvider.GetUtcNow().UtcDateTime;

        logger.LogInformation(
            "Dashboard event {EventName} published by user {UserId}",
            command.EventName,
            userInfo.UserId);

        return Result<PublishDashboardDataResponse>.Success(
            new PublishDashboardDataResponse(command.EventName, publishedAt),
            "Dashboard update published");
    }
}
