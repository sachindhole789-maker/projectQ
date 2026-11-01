using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using App.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rest.API.Configuration;
using Rest.API.Http;
using Rest.API.Middleware;
using Rest.API.Realtime;
using Rest.API.Security;

namespace Rest.API.Extensions;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "Default";
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IEnvironmentInfo, ApiEnvironmentInfo>();
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<IUserInfoProvider, UserInfoProvider>();
        services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHealthChecks();

        services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = !environment.IsProduction();
                options.MaximumReceiveMessageSize = 1024 * 1024;
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNamingPolicy = null);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // The response envelope is contractual: RequestId, Data, Message, Status, StatusCode.
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            // Keep the "HTTP 200 only" contract even for model binding failures.
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors.Select(error =>
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? $"Invalid value for '{entry.Key}'."
                            : error.ErrorMessage))
                    .Distinct();

                var response = ApiResponseWriter.Create(
                    context.HttpContext,
                    AppStatusCodes.ValidationError,
                    string.Join(" | ", errors));

                return new OkObjectResult(response);
            };
        });

        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = storage.MaxFileSizeBytes;
            options.ValueLengthLimit = 1024 * 1024;
        });

        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddOpenApi();
        AddCors(services, configuration, environment);
        AddJwt(services);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
        });
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        AddRateLimiting(services, configuration);

        services.AddHostedService<SignalRDashboardRelay>();
        if (configuration.GetValue("BackgroundJobs:RunDashboardBroadcast", true))
        {
            services.AddHostedService<DashboardBroadcastWorker>();
        }

        return services;
    }

    private static void AddCors(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            policy.AllowAnyHeader().AllowAnyMethod();

            if (!environment.IsProduction() && cors.AllowLocalhostAnyPort)
            {
                policy.SetIsOriginAllowed(origin =>
                    IsLocalhostOrigin(origin)
                    || cors.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase));
            }
            else if (cors.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(cors.AllowedOrigins);
            }

            if (cors.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        }));
    }

    private static void AddJwt(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured through the options system so ISecretResolver can decrypt the
        // signing key and IOptions<JwtOptions> applies the same values as token issuance.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<ISecretResolver, IOptions<JwtOptions>>((options, secrets, jwt) =>
            {
                var signingKey = secrets.Resolve(jwt.Value.SigningKey);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.Value.ClockSkewSeconds),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // SignalR cannot set Authorization headers from browsers/Flutter web.
                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            var token = context.Request.Query["access_token"].FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                context.Token = token;
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var rate = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        if (!rate.Enabled)
        {
            return;
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max((int)Math.Ceiling(retryAfter.TotalSeconds), 1).ToString();
                }

                await ApiResponseWriter.WriteAsync(
                    context.HttpContext,
                    AppStatusCodes.TooManyRequests,
                    "Too many requests. Please wait and retry.");
            };

            // Chained: a request must pass both the per-IP burst guard and the
            // per-user (or per-IP when anonymous) sliding window.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (IsRateLimitExempt(context))
                    {
                        return RateLimitPartition.GetNoLimiter("exempt");
                    }

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"ip:{ClientIp(context)}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = rate.GlobalPermitLimit,
                            Window = TimeSpan.FromSeconds(rate.GlobalWindowSeconds),
                            QueueLimit = rate.GlobalQueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        });
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (IsRateLimitExempt(context))
                    {
                        return RateLimitPartition.GetNoLimiter("exempt");
                    }

                    var userKey = context.User.Identity?.IsAuthenticated == true
                        ? $"user:{context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientIp(context)}"
                        : $"ip:{ClientIp(context)}";

                    return RateLimitPartition.GetSlidingWindowLimiter(
                        userKey,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = rate.UserPermitLimit,
                            Window = TimeSpan.FromSeconds(rate.UserWindowSeconds),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        });
                }));

            // Applied with [EnableRateLimiting("auth")] on login/refresh.
            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ClientIp(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rate.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(rate.AuthWindowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true,
                    }));
        });
    }

    private static bool IsRateLimitExempt(HttpContext context) =>
        HttpMethods.IsOptions(context.Request.Method)
        || context.Request.Path.StartsWithSegments("/hubs")
        || context.Request.Path.StartsWithSegments("/health");

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static bool IsLocalhostOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
