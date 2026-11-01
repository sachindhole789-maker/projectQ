using System.Security.Cryptography;
using System.Text;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a versioned, authenticated payload:
/// <c>enc:v1:{nonce}:{tag}:{cipher}</c> (all Base64).
/// The master key is bootstrap configuration: supply it from the environment
/// in Development/Testing/Production and rotate it deliberately.
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService
{
    private const string Prefix = "enc:";
    private const string VersionTag = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly string[] Parts = [Prefix.TrimEnd(':'), VersionTag];

    private readonly byte[] _key;

    public AesGcmEncryptionService(IOptions<SecurityOptions> options)
    {
        var masterKey = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(masterKey))
        {
            throw new InvalidOperationException(
                "Security:MasterKey is required. Set SECURITY__MASTERKEY to a Base64 32-byte key.");
        }

        try
        {
            _key = Convert.FromBase64String(masterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Security:MasterKey must be valid Base64.", ex);
        }

        if (_key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("Security:MasterKey must decode to 16, 24 or 32 bytes.");
        }
    }

    public bool IsEncrypted(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return string.Join(':',
            Parts[0],
            Parts[1],
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(cipherBytes));
    }

    public string Decrypt(string cipherText)
    {
        if (!IsEncrypted(cipherText))
        {
            throw new ArgumentException("Value is not an encrypted payload.", nameof(cipherText));
        }

        var segments = cipherText.Split(':');
        if (segments.Length != 5
            || !segments[0].Equals(Parts[0], StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals(Parts[1], StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Unsupported encrypted payload format.");
        }

        var nonce = Convert.FromBase64String(segments[2]);
        var tag = Convert.FromBase64String(segments[3]);
        var cipherBytes = Convert.FromBase64String(segments[4]);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
