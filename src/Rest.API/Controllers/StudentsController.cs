using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

public sealed record CreateStudentRequest(
    string? CustomerId,
    string? Name,
    string? Email,
    string? Mobile,
    string? City);

[ApiController]
[Route("api/v1/students")]
[Produces("application/json")]
[Authorize]
[ResponseType<GetStudentResponse>]
[ResponseType<GetStudentsPagedResponse>]
[ResponseType<CreateStudentResponse>]
public sealed class StudentsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetByCustomerId(
        string customerId,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(
            new GetStudentQuery(customerId),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? name,
        [FromQuery] string? email,
        [FromQuery] string? city,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.ExecuteQuery(
            new GetStudentsPagedQuery(name, email, city, pageNumber, pageSize),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<CreateStudentResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new CreateStudentCommand(
                request.CustomerId ?? string.Empty,
                request.Name ?? string.Empty,
                request.Email ?? string.Empty,
                request.Mobile ?? string.Empty,
                request.City ?? string.Empty),
            cancellationToken);

        return result.ToActionResult();
    }
}
