using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Application.Common.Responses;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using Microsoft.Extensions.Options;

namespace App.Application.Features.V1.Files;

public sealed record GetFileQuery(Guid FileId) : IQuery<GetFileResponse>;

public sealed record GetFileResponse(
    Guid FileId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string VirusScanStatus,
    DateTime UploadedAtUtc,
    string DownloadUrl);

public static class FileAccess
{
    /// <summary>Owners and admins only. Adjust here to add tenant/project rules.</summary>
    public static bool CanRead(StoredDocument document, IUserInfoProvider userInfo)
    {
        if (userInfo.UserId is null)
        {
            return false;
        }

        return document.UploadedByUserId == userInfo.UserId
            || userInfo.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
    }

    public static string BuildDownloadUrl(Guid fileId, StorageOptions options) =>
        $"{options.LocalDownloadBasePath.TrimEnd('/')}/{fileId}/content";
}

public sealed class GetFileQueryHandler(
    IFileRepository fileRepository,
    IObjectStorageService storage,
    IUserInfoProvider userInfo,
    IOptions<StorageOptions> storageOptions) : IQueryHandler<GetFileQuery, GetFileResponse>
{
    public async Task<Result<GetFileResponse>> Handle(GetFileQuery query, CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<GetFileResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var document = await fileRepository.GetByIdAsync(query.FileId, cancellationToken);
        if (document is null)
        {
            return Result<GetFileResponse>.Failure(AppStatusCodes.NotFound, "File not found.");
        }

        if (!FileAccess.CanRead(document, userInfo))
        {
            return Result<GetFileResponse>.Failure(AppStatusCodes.Forbidden, "You cannot access this file.");
        }

        var downloadUrl = storage.ProviderName.Equals("Local", StringComparison.OrdinalIgnoreCase)
            ? FileAccess.BuildDownloadUrl(document.FileId, storageOptions.Value)
            : await storage.GetReadUrlAsync(document.StorageKey, cancellationToken);

        return Result<GetFileResponse>.Success(new GetFileResponse(
            document.FileId,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256Hash,
            document.VirusScanStatus,
            document.CreatedAtUtc,
            downloadUrl), "File retrieved successfully");
    }
}
