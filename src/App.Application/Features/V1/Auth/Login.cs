using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace App.Application.Features.V1.Auth;

public sealed record LoginCommand(string Email, string Password, string? IpAddress = null) : ICommand<LoginResponse>;

public sealed record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class LoginCommandHandler(
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    TimeProvider timeProvider,
    ILogger<LoginCommandHandler> logger) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var login = await authRepository.GetLoginInfoAsync(
            command.Email.Trim().ToLowerInvariant(),
            cancellationToken);

        // Same message for unknown user and wrong password: do not leak account existence.
        if (login is null || !login.IsActive || !passwordHasher.Verify(command.Password, login.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}", command.Email);
            return Result<LoginResponse>.Failure(AppStatusCodes.Unauthorized, "Invalid email or password.");
        }

        var roles = login.Roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tokens = jwtTokenService.CreateTokens(login.UserId, login.Email, login.DisplayName, roles);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await authRepository.SaveRefreshTokenAsync(new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = login.UserId,
            TokenHash = jwtTokenService.HashRefreshToken(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = command.IpAddress,
        }, cancellationToken);

        logger.LogInformation("User {UserId} logged in", login.UserId);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.AccessTokenExpiresAtUtc,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc,
            login.UserId,
            login.DisplayName,
            roles), "Login successful");
    }
}
