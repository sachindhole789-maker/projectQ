using App.Application.Abstractions;

namespace Rest.API.Http;

/// <summary>Reads the ambient request identifiers produced by <see cref="RequestIdMiddleware"/>.</summary>
public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public const string RequestIdItemKey = "__request_id";
    public const string CorrelationIdItemKey = "__correlation_id";

    private HttpContext? Context => accessor.HttpContext;

    public string RequestId =>
        Context?.Items[RequestIdItemKey] as string ?? Context?.TraceIdentifier ?? string.Empty;

    public string? CorrelationId => Context?.Items[CorrelationIdItemKey] as string;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers["User-Agent"].FirstOrDefault();
}
