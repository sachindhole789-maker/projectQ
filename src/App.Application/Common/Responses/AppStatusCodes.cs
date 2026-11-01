namespace App.Application.Common.Responses;

/// <summary>
/// Status codes are transported as strings inside the response body. Controller
/// actions always return HTTP 200; the real outcome travels in <c>statusCode</c>.
/// </summary>
public static class AppStatusCodes
{
    public const string Ok = "200";
    public const string BadRequest = "400";
    public const string Unauthorized = "401";
    public const string Forbidden = "403";
    public const string NotFound = "404";
    public const string Conflict = "409";
    public const string ValidationError = "422";
    public const string TooManyRequests = "429";
    public const string ServerError = "500";
}

public static class ApiStatus
{
    public const string Success = "SUCCESS";
    public const string Failure = "FAILURE";
    public const string Error = "ERROR";
}
