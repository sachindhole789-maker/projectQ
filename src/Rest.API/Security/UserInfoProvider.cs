using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Domain.Interfaces.Infrastructure;

namespace Rest.API.Security;

/// <summary>Maps the JWT claims of the current request to the Application abstraction.</summary>
public sealed class UserInfoProvider(IHttpContextAccessor accessor) : IUserInfoProvider
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(raw, out var userId) ? userId : null;
        }
    }

    public string? Email =>
        User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public string? UserName => User?.FindFirst(ClaimTypes.Name)?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
        ?? [];

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
