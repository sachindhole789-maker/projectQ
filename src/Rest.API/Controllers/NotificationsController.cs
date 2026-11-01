using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.Notifications;
using App.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

public sealed record SendNotificationRequest(
    NotificationChannel Channel,
    string? Recipient,
    string? Subject,
    string? Body);

[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
[Authorize]
[ResponseType<SendNotificationResponse>]
public sealed class NotificationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send(
        [FromBody] SendNotificationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<SendNotificationResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new SendNotificationCommand(
                request.Channel,
                request.Recipient ?? string.Empty,
                request.Subject ?? string.Empty,
                request.Body ?? string.Empty),
            cancellationToken);

        return result.ToActionResult();
    }
}
