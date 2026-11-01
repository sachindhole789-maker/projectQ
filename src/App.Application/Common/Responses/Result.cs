using System.Collections;

namespace App.Application.Common.Responses;

/// <summary>
/// Application-level result. Handlers never throw for expected outcomes; they
/// return Success/Failure and the API layer always maps this to HTTP 200.
/// </summary>
public sealed record Result<T>
{
    public T? Data { get; init; }

    /// <summary>Optional second data grid (for procedures that return two grids).</summary>
    public object? Data2 { get; init; }

    public PaginationMeta? Pagination { get; init; }
    public PaginationMeta? Pagination2 { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Status { get; init; } = ApiStatus.Success;
    public string StatusCode { get; init; } = AppStatusCodes.Ok;
    public string RequestId { get; init; } = string.Empty;

    public bool IsSuccess => Status == ApiStatus.Success;

    public static Result<T> Success(T data, string message = "Success") => new()
    {
        Data = data,
        Message = message,
        Status = ApiStatus.Success,
        StatusCode = AppStatusCodes.Ok,
    };

    public static Result<T> SuccessPaginated(
        T data,
        PaginationMeta pagination,
        string message = "Data retrieved successfully") => new()
    {
        Data = data,
        Pagination = pagination,
        Message = message,
        Status = ApiStatus.Success,
        StatusCode = AppStatusCodes.Ok,
    };

    public static Result<T> SuccessPaginated(
        T data1,
        PaginationMeta pagination1,
        object? data2,
        PaginationMeta pagination2,
        string message = "Data retrieved successfully") => new()
    {
        Data = data1,
        Pagination = pagination1,
        Data2 = data2,
        Pagination2 = pagination2,
        Message = message,
        Status = ApiStatus.Success,
        StatusCode = AppStatusCodes.Ok,
    };

    public static Result<T> Failure(string statusCode, string message) => new()
    {
        Data = EmptyValue.For<T>(),
        Message = message,
        Status = ApiStatus.Failure,
        StatusCode = statusCode,
    };

    public static Result<T> Error(string message) => new()
    {
        Data = EmptyValue.For<T>(),
        Message = message,
        Status = ApiStatus.Error,
        StatusCode = AppStatusCodes.ServerError,
    };

    public ApiResponse<T> ToApiResponse() => new(
        RequestId,
        Data,
        Data2,
        Pagination,
        Pagination2,
        Message,
        Status,
        StatusCode);
}

internal static class EmptyValue
{
    /// <summary>Failure payloads expose an empty collection for collection types.</summary>
    public static T? For<T>()
    {
        var type = typeof(T);
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return default;
        }

        if (type.IsArray)
        {
            return (T)(object)Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (!type.IsGenericType)
        {
            return default;
        }

        var definition = type.GetGenericTypeDefinition();
        var isCollection = definition == typeof(List<>)
            || definition == typeof(IList<>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IEnumerable<>);

        if (!isCollection)
        {
            return default;
        }

        var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
        return (T)Activator.CreateInstance(listType)!;
    }
}
