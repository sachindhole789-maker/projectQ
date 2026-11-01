namespace App.Domain.Interfaces.Infrastructure;

/// <summary>
/// Reads the authenticated user from the current request (JWT claims).
/// Implemented in the API layer so Application code stays transport-agnostic.
/// </summary>
public interface IUserInfoProvider
{
    Guid? UserId { get; }
    string? Email { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
}
