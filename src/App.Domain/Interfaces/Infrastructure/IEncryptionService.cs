namespace App.Domain.Interfaces.Infrastructure;

/// <summary>
/// Symmetric encryption for configuration values and sensitive columns.
/// Implementations use AES-GCM with a versioned, authenticated payload.
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);

    bool IsEncrypted(string value);
}
