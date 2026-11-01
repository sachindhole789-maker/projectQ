namespace App.Application.Abstractions;

public sealed record JwtTokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public interface IJwtTokenService
{
    JwtTokenPair CreateTokens(
        Guid userId,
        string email,
        string displayName,
        IEnumerable<string> roles);

    string HashRefreshToken(string refreshToken);
}
