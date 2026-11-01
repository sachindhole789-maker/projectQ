using System.Data;
using App.Application.Common.Options;
using App.Domain.Common;
using App.Domain.Interfaces.Persistence;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IDbManager"/>. Every call opens and
/// disposes its own connection. Provider selection happens in
/// <see cref="DbConnectionFactory"/>.
/// </summary>
public sealed class DbManager : IDbManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly int _commandTimeoutSeconds;
    private readonly ILogger<DbManager> _logger;

    public DbManager(
        IDbConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<DbManager> logger)
    {
        _connectionFactory = connectionFactory;
        _commandTimeoutSeconds = options.Value.CommandTimeoutSeconds;
        _logger = logger;
    }

    public DatabaseProvider Provider => _connectionFactory.Provider;

    public async Task<int> ExecuteAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            procedureName,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));
    }

    public async Task<int> ExecuteTextAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
            procedureName,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<T>(new CommandDefinition(
            procedureName,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<T>> QueryTextAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<T>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));

        return rows.AsList();
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(
            procedureName,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));
    }

    public async Task<T?> ExecuteScalarTextAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));
    }

    public async Task<PagedResult<T>> QueryPagedAsync<T>(
        string procedureName,
        object? parameters,
        int pageNumber,
        int pageSize,
        string? countProcedureName = null,
        CancellationToken cancellationToken = default)
    {
        var pageParams = new DynamicParameters(parameters);
        pageParams.Add("PageNumber", pageNumber);
        pageParams.Add("PageSize", pageSize);

        using var connection = await OpenAsync(cancellationToken);

        // Separate count procedure: one round trip for count, one for the page.
        if (!string.IsNullOrWhiteSpace(countProcedureName))
        {
            var total = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                countProcedureName,
                new DynamicParameters(parameters),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken,
                commandTimeout: _commandTimeoutSeconds)) ?? 0;

            var pageRows = await connection.QueryAsync<T>(new CommandDefinition(
                procedureName,
                pageParams,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken,
                commandTimeout: _commandTimeoutSeconds));

            return new PagedResult<T>(pageRows.AsList(), total, pageNumber, pageSize);
        }

        // One round trip: grid 1 = rows, grid 2 = total count (single value).
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            procedureName,
            pageParams,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds));

        var items = (await grid.ReadAsync<T>()).AsList();
        int totalRecords = items.Count;

        if (!grid.IsConsumed)
        {
            try
            {
                totalRecords = await grid.ReadSingleOrDefaultAsync<int?>() ?? items.Count;
            }
            catch (InvalidOperationException)
            {
                // Procedure only returned one grid; page count is the best fallback.
                totalRecords = items.Count;
            }
        }

        return new PagedResult<T>(items, totalRecords, pageNumber, pageSize);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connectivity check failed for {Provider}", Provider);
            return false;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await action(connection, transaction, cancellationToken);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.Create();
        try
        {
            if (connection is System.Data.Common.DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
