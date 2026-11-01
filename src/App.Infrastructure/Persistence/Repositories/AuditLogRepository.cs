using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;
using Dapper;

namespace App.Infrastructure.Persistence.Repositories;

/// <summary>
/// Audit writes use raw SQL for throughput. The high-volume framework table is
/// intentionally not stored-procedure driven; domain tables still use SPs.
/// </summary>
public sealed class AuditLogRepository(IDbManager dbManager) : IAuditLogRepository
{
    private const string InsertSql = """
        INSERT INTO dbo.AuditLogs
            (RequestId, CorrelationId, UserId, UserName, Method, Path, QueryString,
             IpAddress, UserAgent, HttpStatusCode, DurationMs, RequestBody, ResponseBody, CreatedAtUtc)
        VALUES
            (@RequestId, @CorrelationId, @UserId, @UserName, @Method, @Path, @QueryString,
             @IpAddress, @UserAgent, @HttpStatusCode, @DurationMs, @RequestBody, @ResponseBody, @CreatedAtUtc);
        """;

    public Task WriteAsync(AuditLog log, CancellationToken cancellationToken = default) =>
        dbManager.ExecuteTextAsync(InsertSql, log, cancellationToken);

    public Task WriteBatchAsync(IReadOnlyCollection<AuditLog> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0)
        {
            return Task.CompletedTask;
        }

        return dbManager.ExecuteInTransactionAsync(async (connection, transaction, ct) =>
        {
            foreach (var log in logs)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    InsertSql,
                    log,
                    transaction,
                    cancellationToken: ct));

                await Task.Yield();
            }

            return logs.Count;
        }, cancellationToken);
    }

    public async Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var totalDeleted = 0;
        int deleted;

        do
        {
            deleted = await dbManager.ExecuteTextAsync(
                "DELETE TOP (5000) FROM dbo.AuditLogs WHERE CreatedAtUtc < @CutoffUtc",
                new { CutoffUtc = cutoffUtc },
                cancellationToken);

            totalDeleted += deleted;
        }
        while (deleted > 0 && !cancellationToken.IsCancellationRequested);

        return totalDeleted;
    }
}
