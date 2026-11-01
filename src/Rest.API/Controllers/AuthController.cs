using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.Auth;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record RegisterUserRequest(string? Email, string? Password, string? DisplayName);

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[ResponseType<LoginResponse>]
[ResponseType<RegisterUserResponse>]
public sealed class AuthController(IDispatcher dispatcher, IUserInfoProvider userInfo) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<LoginResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty, userInfo.IpAddress),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<LoginResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new RefreshTokenCommand(request.RefreshToken ?? string.Empty, userInfo.IpAddress),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("register")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<RegisterUserResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new RegisterUserCommand(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                request.DisplayName ?? string.Empty),
            cancellationToken);

        return result.ToActionResult();
    }
}
