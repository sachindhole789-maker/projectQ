namespace App.Domain.Interfaces.Infrastructure;

/// <summary>
/// One-way password hashing (PBKDF2). Verification uses a constant-time compare.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>True when the stored hash was produced with weaker parameters.</summary>
    bool NeedsRehash(string passwordHash);
}
