using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;
using FluentValidation;

namespace App.Application.Features.V1.Auth;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : ICommand<LoginResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenCommandHandler(
    IAuthRepository authRepository,
    IJwtTokenService jwtTokenService,
    TimeProvider timeProvider) : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var currentHash = jwtTokenService.HashRefreshToken(command.RefreshToken);
        var stored = await authRepository.GetRefreshTokenAsync(currentHash, cancellationToken);

        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
        {
            return Result<LoginResponse>.Failure(AppStatusCodes.Unauthorized, "Invalid or expired refresh token.");
        }

        var login = await authRepository.GetLoginInfoByUserIdAsync(stored.UserId, cancellationToken);
        if (login is null || !login.IsActive)
        {
            return Result<LoginResponse>.Failure(AppStatusCodes.Unauthorized, "User is no longer active.");
        }

        // Rotation: the old token can never be reused.
        await authRepository.RevokeRefreshTokenAsync(currentHash, cancellationToken);

        var roles = login.Roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tokens = jwtTokenService.CreateTokens(login.UserId, login.Email, login.DisplayName, roles);

        await authRepository.SaveRefreshTokenAsync(new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = login.UserId,
            TokenHash = jwtTokenService.HashRefreshToken(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = command.IpAddress,
        }, cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.AccessTokenExpiresAtUtc,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc,
            login.UserId,
            login.DisplayName,
            roles), "Token refreshed");
    }
}
