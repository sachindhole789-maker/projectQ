using App.Application.Abstractions;
using App.Domain.Interfaces.Infrastructure;

namespace App.Infrastructure.Security;

public sealed class SecretResolver(IEncryptionService encryption) : ISecretResolver
{
    public string Resolve(string value) =>
        encryption.IsEncrypted(value) ? encryption.Decrypt(value) : value;
}
