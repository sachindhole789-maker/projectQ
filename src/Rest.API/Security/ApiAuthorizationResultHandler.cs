using App.Application.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Rest.API.Http;

namespace Rest.API.Security;

/// <summary>
/// Keeps the HTTP 200 contract for authorization failures. SignalR hubs fall back
/// to the framework handler so clients receive a real 401 for the handshake.
/// </summary>
public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authorizeResult.Challenged)
        {
            await ApiResponseWriter.WriteAsync(
                context,
                AppStatusCodes.Unauthorized,
                "Authentication is required.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await ApiResponseWriter.WriteAsync(
                context,
                AppStatusCodes.Forbidden,
                "You do not have permission to perform this action.");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
