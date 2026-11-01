using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Storage;

/// <summary>Disk-backed storage used by PreDevelopment and tests.</summary>
public sealed class LocalObjectStorageService : IObjectStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalObjectStorageService> _logger;
    private readonly string _rootPath;

    public LocalObjectStorageService(IOptions<StorageOptions> options, ILogger<LocalObjectStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _rootPath = Path.GetFullPath(_options.LocalRootPath);
    }

    public string ProviderName => "Local";

    public async Task<StoredObject> UploadAsync(
        Stream content,
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(request.ObjectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await using (var target = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            }))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var info = new FileInfo(path);
        _logger.LogInformation("Stored {ObjectKey} locally ({SizeBytes} bytes)", request.ObjectKey, info.Length);

        return new StoredObject(request.ObjectKey, request.ContentType, info.Length, $"\"{info.Length}\"");
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(objectKey);
        Stream stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });

        return Task.FromResult(stream);
    }

    public Task<string> GetReadUrlAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult($"{_options.LocalDownloadBasePath.TrimEnd('/')}/{Uri.EscapeDataString(objectKey)}");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(objectKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || Path.IsPathRooted(objectKey))
        {
            throw new ArgumentException("Invalid object key.", nameof(objectKey));
        }

        var normalizedKey = objectKey.Replace('\\', '/');
        if (normalizedKey.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("Path traversal is not allowed.", nameof(objectKey));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Object key escapes the storage root.", nameof(objectKey));
        }

        return fullPath;
    }
}
