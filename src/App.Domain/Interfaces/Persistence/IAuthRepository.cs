using App.Domain.Dtos.Auth;
using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface IAuthRepository
{
    Task<LoginInfoDto?> GetLoginInfoAsync(string email, CancellationToken cancellationToken = default);

    Task<LoginInfoDto?> GetLoginInfoByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task CreateUserAsync(User user, CancellationToken cancellationToken = default);

    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
}
