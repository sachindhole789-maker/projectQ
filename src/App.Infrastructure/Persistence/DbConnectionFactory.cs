using System.Data;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace App.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    DatabaseProvider Provider { get; }

    IDbConnection Create();
}

/// <summary>
/// Creates a driver-specific connection from configuration. AWS RDS uses the
/// SQL Server driver (point the connection string at the RDS endpoint) unless
/// Provider is set to Oracle.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IOptions<DatabaseOptions> options, ISecretResolver secretResolver)
    {
        var value = options.Value;
        var connectionString = value.ConnectionString
            ?? throw new InvalidOperationException("Database:ConnectionString is not configured.");

        // The whole connection string may be stored as an enc:v1:... value so
        // database credentials never appear in plaintext configuration.
        _connectionString = secretResolver.Resolve(connectionString);

        Provider = value.Provider.Trim().ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
            "awsrds" or "rds" => DatabaseProvider.AwsRds,
            "oracle" => DatabaseProvider.Oracle,
            _ => throw new InvalidOperationException($"Unsupported database provider '{value.Provider}'."),
        };
    }

    public DatabaseProvider Provider { get; }

    public IDbConnection Create() => Provider switch
    {
        DatabaseProvider.SqlServer or DatabaseProvider.AwsRds => new SqlConnection(_connectionString),
        DatabaseProvider.Oracle => new OracleConnection(_connectionString),
        _ => throw new InvalidOperationException($"Unsupported database provider '{Provider}'."),
    };
}
