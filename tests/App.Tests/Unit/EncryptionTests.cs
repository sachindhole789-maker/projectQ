using App.Application.Common.Options;
using App.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace App.Tests.Unit;

public sealed class EncryptionTests
{
    private static AesGcmEncryptionService CreateService() =>
        new(Options.Create(new SecurityOptions
        {
            MasterKey = Convert.ToBase64String(new byte[32]),
        }));

    [Fact]
    public void Encrypt_then_decrypt_round_trips()
    {
        var service = CreateService();

        var cipherText = service.Encrypt("smtp-password");

        Assert.True(service.IsEncrypted(cipherText));
        Assert.StartsWith("enc:v1:", cipherText);
        Assert.Equal("smtp-password", service.Decrypt(cipherText));
    }

    [Fact]
    public void Encrypt_uses_a_fresh_nonce_each_time()
    {
        var service = CreateService();

        var first = service.Encrypt("same-value");
        var second = service.Encrypt("same-value");

        Assert.NotEqual(first, second);
        Assert.Equal("same-value", service.Decrypt(first));
        Assert.Equal("same-value", service.Decrypt(second));
    }

    [Fact]
    public void Decrypt_rejects_tampered_payloads()
    {
        var service = CreateService();
        var cipherText = service.Encrypt("secret");
        var parts = cipherText.Split(':');
        var cipherBytes = Convert.FromBase64String(parts[4]);
        cipherBytes[0] ^= 0xFF;
        var tampered = string.Join(':', parts[0], parts[1], parts[2], parts[3], Convert.ToBase64String(cipherBytes));

        Assert.ThrowsAny<Exception>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Plaintext_values_are_not_reported_as_encrypted()
    {
        var service = CreateService();

        Assert.False(service.IsEncrypted("plain-value"));
        Assert.False(service.IsEncrypted(string.Empty));
    }
}
