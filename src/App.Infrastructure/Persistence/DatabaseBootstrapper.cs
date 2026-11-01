using System.Data.Common;
using System.Text;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Common;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Applies a locally supplied SQL Server schema and an optional admin seed on
/// startup. Schema and stored procedures are not part of the repository; place
/// your script at db/sqlserver/001_Schema.sql to embed and apply it. Never runs
/// in Production.
/// </summary>
public sealed class DatabaseBootstrapper(
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<SeedOptions> seedOptions,
    IDbConnectionFactory connectionFactory,
    ISecretResolver secretResolver,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<DatabaseBootstrapper> logger) : IHostedService
{
    private readonly DatabaseOptions _database = databaseOptions.Value;
    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_database.InitializeOnStartup && !_database.SeedOnStartup)
        {
            return;
        }

        if (environment.IsProduction())
        {
            logger.LogWarning("Database bootstrap is disabled in Production.");
            return;
        }

        if (connectionFactory.Provider == DatabaseProvider.Oracle)
        {
            logger.LogWarning("Automatic schema creation is not supported for Oracle; run /db/oracle scripts manually.");
            return;
        }

        if (_database.InitializeOnStartup)
        {
            var schemaApplied = await ApplySchemaAsync(cancellationToken);
            if (!schemaApplied)
            {
                return;
            }
        }

        if (_database.SeedOnStartup)
        {
            await SeedAdminAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> ApplySchemaAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(DatabaseBootstrapper).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Schema.sql", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            logger.LogWarning(
                "No database schema is embedded. Schema and stored procedures are not " +
                "published with this repository; place your script at " +
                "db/sqlserver/001_Schema.sql and rebuild, or apply it manually and set " +
                "Database:InitializeOnStartup to false.");
            return false;
        }

        await using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} could not be opened.");

        using var reader = new StreamReader(resource);
        var script = await reader.ReadToEndAsync(cancellationToken);

        await using DbConnection connection = (DbConnection)connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var batchCount = 0;
        foreach (var batch in SplitSqlBatches(script))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                batch,
                cancellationToken: cancellationToken,
                commandTimeout: 180));

            batchCount++;
        }

        logger.LogInformation("Database schema applied ({BatchCount} batches).", batchCount);
        return true;
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.AdminPassword))
        {
            logger.LogWarning(
                "Seed:AdminPassword is empty. Set SEED__ADMINPASSWORD to create the initial admin user.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var authRepository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = _seed.AdminEmail.Trim().ToLowerInvariant();
        if (await authRepository.EmailExistsAsync(email, cancellationToken))
        {
            logger.LogInformation("Seed admin {Email} already exists.", email);
            return;
        }

        var adminPassword = secretResolver.Resolve(_seed.AdminPassword);
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Seed:AdminPassword resolved to an empty value; skipping admin seed.");
            return;
        }

        await authRepository.CreateUserAsync(new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(adminPassword),
            DisplayName = _seed.AdminDisplayName,
            Roles = "Admin,User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        logger.LogInformation("Seeded admin user {Email}.", email);
    }

    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        var buffer = new StringBuilder();

        using var reader = new StringReader(script);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString();
                    buffer.Clear();
                }

                continue;
            }

            buffer.AppendLine(line);
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
