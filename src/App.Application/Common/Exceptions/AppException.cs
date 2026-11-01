using App.Application.Common.Responses;

namespace App.Application.Common.Exceptions;

/// <summary>Expected, client-safe failure raised outside the Result flow.</summary>
public sealed class AppException : Exception
{
    public string StatusCode { get; }

    public AppException(string message, string statusCode = AppStatusCodes.BadRequest, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
