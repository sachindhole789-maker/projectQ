using System.Security.Cryptography;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256. Format: PBKDF2-SHA256$iterations$salt$hash.
/// 210,000 iterations is the current OWASP recommendation for SHA-256.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher(IOptions<SecurityOptions> options)
    {
        _iterations = Math.Max(options.Value.Pbkdf2Iterations, 100_000);
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join('$',
            Scheme,
            _iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 4
            || !parts[0].Equals(Scheme, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || !parts[0].Equals(Scheme, StringComparison.Ordinal))
        {
            return true;
        }

        return !int.TryParse(parts[1], out var iterations) || iterations < _iterations;
    }
}
