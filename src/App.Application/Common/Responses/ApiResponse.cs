namespace App.Application.Common.Responses;

/// <summary>
/// The single response shape every controller returns.
/// RequestId, Data, Message, Status and StatusCode are always present.
/// </summary>
public sealed record ApiResponse<T>(
    string RequestId,
    T? Data,
    object? Data2,
    PaginationMeta? Pagination,
    PaginationMeta? Pagination2,
    string Message,
    string Status,
    string StatusCode);
