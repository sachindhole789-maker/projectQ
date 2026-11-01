using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface IFileRepository
{
    Task CreateAsync(StoredDocument document, CancellationToken cancellationToken = default);

    Task<StoredDocument?> GetByIdAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task UpdateVirusScanStatusAsync(Guid fileId, string status, CancellationToken cancellationToken = default);
}
