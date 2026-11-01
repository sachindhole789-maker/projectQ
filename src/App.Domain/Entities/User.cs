namespace App.Domain.Entities;

public sealed class User
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by <c>IPasswordHasher</c>. Never store plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Comma separated role list (e.g. "Admin,User").</summary>
    public string Roles { get; set; } = "User";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
