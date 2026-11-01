namespace Rest.API.Http;

/// <summary>
/// Copies or creates a request id, stores it in HttpContext.Items and returns it
/// in the X-Request-Id response header so clients can quote it in support tickets.
/// </summary>
public sealed class RequestIdMiddleware(RequestDelegate next)
{
    private const int MaxIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        var requestId = IsUsable(incoming) ? incoming! : context.TraceIdentifier;

        context.TraceIdentifier = requestId;
        context.Items[RequestContext.RequestIdItemKey] = requestId;

        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (IsUsable(correlationId))
        {
            context.Items[RequestContext.CorrelationIdItemKey] = correlationId!;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-Id"] = requestId;
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxIdLength
        && value.All(character => !char.IsControl(character));
}
