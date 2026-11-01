namespace App.Domain.Interfaces.Infrastructure;

public sealed record StorageUploadRequest(
    string ObjectKey,
    string ContentType,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record StoredObject(
    string ObjectKey,
    string ContentType,
    long SizeBytes,
    string? ETag);

/// <summary>
/// Object storage abstraction. AWS S3 in Development/Testing/Production and a
/// local disk implementation for PreDevelopment and tests.
/// </summary>
public interface IObjectStorageService
{
    string ProviderName { get; }

    Task<StoredObject> UploadAsync(
        Stream content,
        StorageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(string objectKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
