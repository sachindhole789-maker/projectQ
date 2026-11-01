using System.Text.Json;
using App.Application.Common.Responses;

namespace Rest.API.Http;

/// <summary>
/// Writes the common response envelope for failures raised outside controllers
/// (authentication challenges, rate limiting, unhandled exceptions) while keeping
/// the HTTP status at 200, matching the API contract.
/// </summary>
public static class ApiResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    public static ApiResponse<object?> Create(
        HttpContext context,
        string statusCode,
        string message,
        string status = ApiStatus.Failure) =>
        new(
            context.Items[RequestContext.RequestIdItemKey] as string ?? context.TraceIdentifier,
            null,
            null,
            null,
            null,
            message,
            status,
            statusCode);

    public static async Task WriteAsync(
        HttpContext context,
        string statusCode,
        string message,
        string status = ApiStatus.Failure)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(Create(context, statusCode, message, status), SerializerOptions),
            context.RequestAborted);
    }
}
