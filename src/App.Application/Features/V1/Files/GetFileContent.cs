using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;

namespace App.Application.Features.V1.Files;

public sealed record GetFileContentQuery(Guid FileId) : IQuery<FileContentResponse>;

public sealed record FileContentResponse(Stream Content, string FileName, string ContentType);

public sealed class GetFileContentQueryHandler(
    IFileRepository fileRepository,
    IObjectStorageService storage,
    IUserInfoProvider userInfo) : IQueryHandler<GetFileContentQuery, FileContentResponse>
{
    public async Task<Result<FileContentResponse>> Handle(
        GetFileContentQuery query,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<FileContentResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var document = await fileRepository.GetByIdAsync(query.FileId, cancellationToken);
        if (document is null)
        {
            return Result<FileContentResponse>.Failure(AppStatusCodes.NotFound, "File not found.");
        }

        if (!FileAccess.CanRead(document, userInfo))
        {
            return Result<FileContentResponse>.Failure(AppStatusCodes.Forbidden, "You cannot access this file.");
        }

        if (!document.VirusScanStatus.Equals("Clean", StringComparison.OrdinalIgnoreCase))
        {
            return Result<FileContentResponse>.Failure(
                AppStatusCodes.ValidationError,
                "This file has not passed the malware scan.");
        }

        var stream = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        return Result<FileContentResponse>.Success(
            new FileContentResponse(stream, document.OriginalFileName, document.ContentType),
            "File content retrieved");
    }
}
