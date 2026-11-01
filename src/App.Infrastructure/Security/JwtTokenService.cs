using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Application.Abstractions;
using App.Application.Common.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace App.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKeyBytes;

    public JwtTokenService(IOptions<JwtOptions> options, ISecretResolver secretResolver, TimeProvider timeProvider)
    {
        _options = options.Value;
        TimeProvider = timeProvider;

        var signingKey = secretResolver.Resolve(_options.SigningKey);
        if (signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must resolve to at least 32 characters (256 bits) for HS256.");
        }

        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
    }

    private TimeProvider TimeProvider { get; }

    public JwtTokenPair CreateTokens(
        Guid userId,
        string email,
        string displayName,
        IEnumerable<string> roles)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.Name, displayName),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_signingKeyBytes),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(token);
        var refreshToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

        return new JwtTokenPair(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
