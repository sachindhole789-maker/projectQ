using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;

namespace App.Infrastructure.Persistence.Repositories;

public sealed class FileRepository(IDbManager dbManager) : IFileRepository
{
    public Task CreateAsync(StoredDocument document, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteAsync("sp_File_Create", new
        {
            document.FileId,
            document.OriginalFileName,
            document.StorageKey,
            document.ContentType,
            document.DetectedContentType,
            document.SizeBytes,
            document.Sha256Hash,
            document.UploadedByUserId,
            document.Environment,
            document.VirusScanStatus,
            document.CreatedAtUtc,
        }, cancellationToken);

    public Task<StoredDocument?> GetByIdAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<StoredDocument>(
            "sp_File_GetById",
            new { FileId = fileId },
            cancellationToken);

    public Task UpdateVirusScanStatusAsync(
        Guid fileId,
        string status,
        CancellationToken cancellationToken = default) =>
        dbManager.ExecuteAsync(
            "sp_File_UpdateVirusScanStatus",
            new { FileId = fileId, VirusScanStatus = status },
            cancellationToken);
}
