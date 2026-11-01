using System.Security.Cryptography;
using App.Application;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Interfaces.Infrastructure;
using App.Infrastructure.Extensions;
using App.Infrastructure.Security;
using App.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var command = args.FirstOrDefault()?.ToLowerInvariant();

if (command is not null)
{
    return RunUtility(command, args);
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

// The runner owns outbox dispatch and audit retention. API instances keep the
// audit writer but must not compete for the notification outbox.
builder.Configuration["BackgroundJobs:RunAuditWriter"] = "false";
builder.Configuration["BackgroundJobs:RunNotificationDispatcher"] = "true";
builder.Configuration["BackgroundJobs:RunAuditRetention"] = "true";
builder.Configuration["BackgroundJobs:RunDashboardBroadcast"] = "false";

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IEnvironmentInfo, RunnerEnvironmentInfo>();
builder.Services.AddSingleton<IRealtimePublisher, NullRealtimePublisher>();

var host = builder.Build();
await host.RunAsync();
return 0;

static int RunUtility(string command, string[] args)
{
    if (command is "help" or "--help" or "-h")
    {
        PrintUsage();
        return 0;
    }

    var configuration = BuildUtilityConfiguration();
    var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
        ?? new SecurityOptions();

    switch (command)
    {
        case "generate-key":
        case "gen-key":
        {
            var length = 32;
            if (args.Length > 1 && int.TryParse(args[1], out var requested))
            {
                length = requested;
            }

            if (length is not (16 or 24 or 32))
            {
                Console.Error.WriteLine("Key length must be 16, 24 or 32 bytes.");
                return 1;
            }

            Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(length)));
            return 0;
        }

        case "encrypt":
        {
            if (string.IsNullOrWhiteSpace(security.MasterKey))
            {
                Console.Error.WriteLine(
                    "Security__MasterKey is not configured. Set the environment variable " +
                    "or add Security:MasterKey to the environment appsettings file.");
                return 1;
            }

            var plainText = args.Length > 1
                ? string.Join(' ', args.Skip(1))
                : ReadSecret("Value to encrypt");

            if (string.IsNullOrWhiteSpace(plainText))
            {
                Console.Error.WriteLine("Nothing to encrypt.");
                return 1;
            }

            var encryption = new AesGcmEncryptionService(Options.Create(security));
            Console.WriteLine(encryption.Encrypt(plainText));
            return 0;
        }

        case "hash":
        {
            var password = args.Length > 1
                ? args[1]
                : Environment.GetEnvironmentVariable("PASSWORD") ?? ReadSecret("Password to hash");

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("Nothing to hash.");
                return 1;
            }

            var hasher = new Pbkdf2PasswordHasher(Options.Create(security));
            Console.WriteLine(hasher.Hash(password));
            return 0;
        }

        default:
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintUsage();
            return 1;
    }
}

static IConfiguration BuildUtilityConfiguration()
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environments.Development;

    return new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddJsonFile($"appsettings.{environment}.local.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
}

static string? ReadSecret(string prompt)
{
    Console.Write($"{prompt}: ");
    return Console.ReadLine();
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        App.Runner commands:
          (no arguments)        Runs notification dispatch and audit retention workers.
          generate-key [16|24|32]  Prints a Base64 AES master key (default 32 bytes).
          encrypt <plaintext>   Prints an enc:v1 AES-GCM value for the active environment key.
          hash <password>       Prints a PBKDF2-SHA256 password hash for seeding.

        Set DOTNET_ENVIRONMENT to PreDevelopment, Development, Testing or Production.
        """);
}
