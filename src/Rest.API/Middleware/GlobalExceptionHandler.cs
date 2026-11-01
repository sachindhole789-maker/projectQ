using App.Application.Common.Exceptions;
using App.Application.Common.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Rest.API.Http;

namespace Rest.API.Middleware;

/// <summary>
/// Converts unexpected exceptions into the common response envelope. Expected
/// <see cref="AppException"/> failures keep their status code in the body.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        if (exception is AppException appException)
        {
            logger.LogWarning(exception, "Handled application exception for {Path}", httpContext.Request.Path);
            await ApiResponseWriter.WriteAsync(httpContext, appException.StatusCode, appException.Message);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        await ApiResponseWriter.WriteAsync(
            httpContext,
            AppStatusCodes.ServerError,
            "An unexpected error occurred. Quote the X-Request-Id when contacting support.",
            ApiStatus.Error);

        return true;
    }
}
