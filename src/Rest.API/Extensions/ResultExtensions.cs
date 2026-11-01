using App.Application.Common.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Rest.API.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Maps an Application result to HTTP 200 with the outcome carried in the body
    /// (statusCode is a string, e.g. "200", "404", "422").
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        new OkObjectResult(result.ToApiResponse());
}
