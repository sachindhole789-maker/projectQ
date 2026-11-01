using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Storage;

/// <summary>
/// AWS S3 storage. Credentials come from the default AWS chain (IAM role,
/// profile or environment) unless static keys are explicitly configured.
/// Objects are encrypted server-side by default and never made public.
/// </summary>
public sealed class S3ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly StorageOptions _options;
    private readonly ILogger<S3ObjectStorageService> _logger;

    public S3ObjectStorageService(
        IOptions<StorageOptions> options,
        ISecretResolver secretResolver,
        ILogger<S3ObjectStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ForcePathStyle = _options.ForcePathStyle,
            RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region),
        };

        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            config.ServiceURL = _options.ServiceUrl;
        }

        var accessKey = string.IsNullOrWhiteSpace(_options.AccessKey)
            ? null
            : secretResolver.Resolve(_options.AccessKey);

        var secretKey = string.IsNullOrWhiteSpace(_options.SecretKey)
            ? string.Empty
            : secretResolver.Resolve(_options.SecretKey);

        _client = accessKey is not null
            ? new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config)
            : new AmazonS3Client(config);
    }

    public string ProviderName => "S3";

    public async Task<StoredObject> UploadAsync(
        Stream content,
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var key = BuildKey(request.ObjectKey);
        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = request.ContentType,
            AutoCloseStream = false,
            ServerSideEncryptionMethod = _options.UseServerSideEncryption
                ? (string.IsNullOrWhiteSpace(_options.KmsKeyId)
                    ? ServerSideEncryptionMethod.AES256
                    : ServerSideEncryptionMethod.AWSKMS)
                : null,
            ServerSideEncryptionKeyManagementServiceKeyId = string.IsNullOrWhiteSpace(_options.KmsKeyId)
                ? null
                : _options.KmsKeyId,
        };

        if (request.Metadata is not null)
        {
            foreach (var (metadataKey, metadataValue) in request.Metadata)
            {
                putRequest.Metadata.Add(metadataKey, metadataValue);
            }
        }

        var response = await _client.PutObjectAsync(putRequest, cancellationToken);
        var size = content.CanSeek ? content.Length : 0;

        _logger.LogInformation("Uploaded {ObjectKey} to bucket {Bucket} ({SizeBytes} bytes)", key, _options.BucketName, size);

        return new StoredObject(key, request.ContentType, size, response.ETag);
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = BuildKey(objectKey),
        }, cancellationToken);

        return new OwnedResponseStream(response.ResponseStream, response);
    }

    public Task<string> GetReadUrlAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = BuildKey(objectKey),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.PresignedUrlMinutes),
        });

        return Task.FromResult(url);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = BuildKey(objectKey),
        }, cancellationToken);
    }

    private string BuildKey(string objectKey)
    {
        var prefix = _options.Prefix?.Trim('/');
        var key = objectKey.TrimStart('/');
        return string.IsNullOrWhiteSpace(prefix) ? key : $"{prefix}/{key}";
    }

    public void Dispose() => _client.Dispose();

    private sealed class OwnedResponseStream(Stream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
