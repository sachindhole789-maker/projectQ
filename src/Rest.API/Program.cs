using App.Application;
using App.Application.Common.Options;
using App.Domain.Constants;
using App.Infrastructure.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Rest.API.Extensions;
using Rest.API.Http;
using Rest.API.Realtime;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Git-ignored local overrides keep keys and connection strings out of source
// control. The file is optional, so clones without it still build and start.
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var maxUploadBytes = builder.Configuration.GetValue<long?>(
    $"{StorageOptions.SectionName}:MaxFileSizeBytes") ?? 26_214_400;

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = maxUploadBytes + 1_048_576;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

if (!EnvironmentNames.All.Contains(app.Environment.EnvironmentName, StringComparer.OrdinalIgnoreCase))
{
    app.Logger.LogWarning(
        "Environment '{Environment}' is not one of {KnownEnvironments}. " +
        "Set ASPNETCORE_ENVIRONMENT to PreDevelopment, Development, Testing or Production.",
        app.Environment.EnvironmentName,
        string.Join(", ", EnvironmentNames.All));
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
        diagnosticContext.Set(
            "CorrelationId",
            httpContext.Items[RequestContext.CorrelationIdItemKey] as string ?? string.Empty);
        diagnosticContext.Set(
            "ClientIp",
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
});

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
app.UseAuthentication();

var rateLimitingEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/", () => Results.Ok(new
{
    Service = "Rest.API",
    Environment = app.Environment.EnvironmentName,
    ServerTimeUtc = DateTime.UtcNow,
})).AllowAnonymous();

app.Run();

public partial class Program;
