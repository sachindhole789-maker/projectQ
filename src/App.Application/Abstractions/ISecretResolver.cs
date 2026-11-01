namespace App.Application.Abstractions;

/// <summary>Decrypts <c>enc:</c> prefixed values; leaves plaintext untouched.</summary>
public interface ISecretResolver
{
    string Resolve(string value);
}
