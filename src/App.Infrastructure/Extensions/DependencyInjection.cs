using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using App.Infrastructure.Audit;
using App.Infrastructure.BackgroundJobs;
using App.Infrastructure.Notifications;
using App.Infrastructure.Persistence;
using App.Infrastructure.Persistence.Repositories;
using App.Infrastructure.Security;
using App.Infrastructure.Storage;
using App.Infrastructure.VirusScanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddOptions(services, configuration);
        AddPersistence(services);
        AddSecurity(services);
        AddStorage(services, configuration);
        AddNotifications(services, configuration);
        AddBackgroundJobs(services, configuration);
        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Database:ConnectionString is required.")
            .Validate(o => o.CommandTimeoutSeconds > 0, "Database:CommandTimeoutSeconds must be positive.")
            .ValidateOnStart();

        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(o => IsValidMasterKey(o.MasterKey),
                "Security:MasterKey must be a Base64-encoded 16/24/32-byte key.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey is required.")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 1440,
                "Jwt:AccessTokenMinutes must be between 1 and 1440.")
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(o => o.MaxFileSizeBytes > 0, "Storage:MaxFileSizeBytes must be positive.")
            .Validate(o => o.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(o.BucketName),
                "Storage:BucketName is required when Storage:Provider is S3.")
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => !o.Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.FromEmail)),
                "Email host and FromEmail are required for the SMTP provider.")
            .ValidateOnStart();

        services.AddOptions<AuditLogOptions>()
            .Bind(configuration.GetSection(AuditLogOptions.SectionName))
            .Validate(o => o.QueueCapacity > 0 && o.BatchSize > 0, "AuditLog queue/batch sizes must be positive.")
            .ValidateOnStart();

        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .Validate(o => o.BatchSize > 0 && o.MaxAttempts > 0, "Notification batch size and max attempts must be positive.")
            .ValidateOnStart();

        services.AddOptions<BackgroundJobOptions>()
            .Bind(configuration.GetSection(BackgroundJobOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<VirusScanOptions>()
            .Bind(configuration.GetSection(VirusScanOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .ValidateOnStart();
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IDbManager, DbManager>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddHostedService<DatabaseBootstrapper>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
        services.AddSingleton<ISecretResolver, SecretResolver>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{StorageOptions.SectionName}:Provider"] ?? "Local";

        if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();
        }
        else
        {
            services.AddSingleton<IObjectStorageService, LocalObjectStorageService>();
        }

        var scanProvider = configuration[$"{VirusScanOptions.SectionName}:Provider"] ?? "None";
        if (scanProvider.Equals("ClamAV", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IVirusScanner, ClamAvVirusScanner>();
        }
        else
        {
            services.AddSingleton<IVirusScanner, NoOpVirusScanner>();
        }
    }

    private static void AddNotifications(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Log";

        if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        services.AddSingleton<IAuditLogQueue, AuditLogChannel>();
    }

    private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue("BackgroundJobs:RunAuditWriter", true))
        {
            services.AddHostedService<AuditLogWriterService>();
        }

        if (configuration.GetValue("Notifications:Enabled", true)
            && configuration.GetValue("BackgroundJobs:RunNotificationDispatcher", false))
        {
            services.AddHostedService<NotificationDispatchWorker>();
        }

        if (configuration.GetValue("BackgroundJobs:RunAuditRetention", false))
        {
            services.AddHostedService<AuditRetentionWorker>();
        }
    }

    private static bool IsValidMasterKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length is 16 or 24 or 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
