using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

public sealed record PublishDashboardRequest(
    string? EventName,
    Dictionary<string, object?>? Payload);

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
[Authorize]
[ResponseType<DashboardSnapshotResponse>]
[ResponseType<PublishDashboardDataResponse>]
public sealed class DashboardController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(
            new GetDashboardSnapshotQuery(),
            cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Publishes any payload to every connected SignalR dashboard client.
    /// </summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(
        [FromBody] PublishDashboardRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<PublishDashboardDataResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new PublishDashboardDataCommand(
                request.EventName ?? string.Empty,
                request.Payload ?? new Dictionary<string, object?>()),
            cancellationToken);

        return result.ToActionResult();
    }
}
