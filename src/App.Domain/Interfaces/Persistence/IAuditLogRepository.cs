using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface IAuditLogRepository
{
    Task WriteAsync(AuditLog log, CancellationToken cancellationToken = default);

    Task WriteBatchAsync(IReadOnlyCollection<AuditLog> logs, CancellationToken cancellationToken = default);

    Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
