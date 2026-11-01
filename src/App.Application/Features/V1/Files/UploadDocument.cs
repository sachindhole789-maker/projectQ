using App.Application.Abstractions;
using App.Application.Common.Exceptions;
using App.Application.Common.Responses;
using App.Application.Services;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace App.Application.Features.V1.Files;

public sealed record UploadDocumentCommand(Stream Content, string FileName, string? ContentType)
    : ICommand<UploadDocumentResponse>;

public sealed record UploadDocumentResponse(
    Guid FileId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string VirusScanStatus,
    string DownloadUrl);

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).MaximumLength(150).When(x => x.ContentType is not null);
    }
}

public sealed class UploadDocumentCommandHandler(
    ILogger<UploadDocumentCommandHandler> logger,
    IDocumentValidationService documentValidation,
    IVirusScanner virusScanner,
    IObjectStorageService storage,
    IFileRepository fileRepository,
    IUserInfoProvider userInfo,
    IEnvironmentInfo environment,
    TimeProvider timeProvider) : ICommandHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    public async Task<Result<UploadDocumentResponse>> Handle(
        UploadDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = userInfo.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            return Result<UploadDocumentResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        string? uploadedKey = null;

        try
        {
            ValidatedDocument validated = await documentValidation.ValidateAsync(
                command.Content,
                command.FileName,
                command.ContentType,
                cancellationToken);

            command.Content.Position = 0;
            VirusScanResult scan = await virusScanner.ScanAsync(
                command.Content,
                validated.FileName,
                cancellationToken);

            if (!scan.IsClean)
            {
                logger.LogWarning(
                    "Rejected upload {FileName} from user {UserId}: {Threat}",
                    validated.FileName,
                    userId,
                    scan.ThreatName);

                return Result<UploadDocumentResponse>.Failure(
                    AppStatusCodes.ValidationError,
                    "The file was rejected by the malware scanner.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var objectKey = $"{environment.Name.ToLowerInvariant()}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{validated.Extension}";

            command.Content.Position = 0;
            StoredObject stored = await storage.UploadAsync(
                command.Content,
                new StorageUploadRequest(
                    objectKey,
                    validated.DetectedContentType,
                    new Dictionary<string, string>
                    {
                        ["original-name"] = validated.FileName,
                        ["sha256"] = validated.Sha256Hash,
                        ["uploaded-by"] = userId.Value.ToString(),
                    }),
                cancellationToken);

            uploadedKey = stored.ObjectKey;

            var document = new StoredDocument
            {
                FileId = Guid.NewGuid(),
                OriginalFileName = validated.FileName,
                StorageKey = stored.ObjectKey,
                ContentType = validated.DetectedContentType,
                DetectedContentType = validated.DetectedContentType,
                SizeBytes = validated.SizeBytes,
                Sha256Hash = validated.Sha256Hash,
                UploadedByUserId = userId,
                Environment = environment.Name,
                VirusScanStatus = "Clean",
                CreatedAtUtc = now,
            };

            await fileRepository.CreateAsync(document, cancellationToken);

            var downloadUrl = await storage.GetReadUrlAsync(stored.ObjectKey, cancellationToken);
            logger.LogInformation(
                "Stored document {FileId} ({SizeBytes} bytes) for user {UserId}",
                document.FileId,
                document.SizeBytes,
                userId);

            return Result<UploadDocumentResponse>.Success(new UploadDocumentResponse(
                document.FileId,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256Hash,
                document.VirusScanStatus,
                downloadUrl), "File uploaded successfully");
        }
        catch (AppException ex)
        {
            return Result<UploadDocumentResponse>.Failure(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for user {UserId}", userId);

            if (uploadedKey is not null)
            {
                try
                {
                    await storage.DeleteAsync(uploadedKey, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    logger.LogWarning(cleanupException, "Could not clean up orphaned object {ObjectKey}", uploadedKey);
                }
            }

            return Result<UploadDocumentResponse>.Error("File upload failed.");
        }
    }
}
