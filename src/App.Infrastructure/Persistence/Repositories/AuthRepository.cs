using App.Domain.Dtos.Auth;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;

namespace App.Infrastructure.Persistence.Repositories;

public sealed class AuthRepository(IDbManager dbManager) : IAuthRepository
{
    public Task<LoginInfoDto?> GetLoginInfoAsync(string email, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<LoginInfoDto>(
            "sp_Auth_GetLoginInfoByEmail",
            new { Email = email },
            cancellationToken);

    public Task<LoginInfoDto?> GetLoginInfoByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<LoginInfoDto>(
            "sp_Auth_GetLoginInfoByUserId",
            new { UserId = userId },
            cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await dbManager.ExecuteScalarAsync<int>(
            "sp_Auth_EmailExists",
            new { Email = email },
            cancellationToken) == 1;

    public Task CreateUserAsync(User user, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteAsync("sp_Auth_CreateUser", new
        {
            user.UserId,
            user.Email,
            user.PasswordHash,
            user.DisplayName,
            user.Roles,
            user.IsActive,
            user.CreatedAtUtc,
        }, cancellationToken);

    public Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteAsync("sp_Auth_SaveRefreshToken", new
        {
            token.TokenId,
            token.UserId,
            token.TokenHash,
            token.ExpiresAtUtc,
            token.CreatedAtUtc,
            token.CreatedByIp,
        }, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<RefreshToken>(
            "sp_Auth_GetRefreshToken",
            new { TokenHash = tokenHash },
            cancellationToken);

    public Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteAsync(
            "sp_Auth_RevokeRefreshToken",
            new { TokenHash = tokenHash },
            cancellationToken);
}
