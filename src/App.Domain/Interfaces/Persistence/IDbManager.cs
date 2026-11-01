using System.Data;
using App.Domain.Common;

namespace App.Domain.Interfaces.Persistence;

/// <summary>
/// Provider-agnostic Dapper data access.
/// Stored-procedure methods open a connection, execute, and dispose it; no
/// connection is kept alive between calls. Switch provider and connection string
/// per environment in configuration.
/// </summary>
public interface IDbManager
{
    DatabaseProvider Provider { get; }

    /// <summary>Execute a stored procedure (INSERT/UPDATE/DELETE). Returns affected rows.</summary>
    Task<int> ExecuteAsync(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Execute raw SQL text. Use only for framework tables or scripts.</summary>
    Task<int> ExecuteTextAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryTextAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarTextAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a paged stored procedure.
    /// Contract: pass @PageNumber and @PageSize (added automatically). The
    /// procedure should return page rows in the first result set and total row
    /// count in a second single-column result set. When <paramref name="countProcedureName"/>
    /// is supplied, that procedure is executed separately for the count.
    /// </summary>
    Task<PagedResult<T>> QueryPagedAsync<T>(
        string procedureName,
        object? parameters,
        int pageNumber,
        int pageSize,
        string? countProcedureName = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Advanced escape hatch for multi-statement transactional work.</summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
