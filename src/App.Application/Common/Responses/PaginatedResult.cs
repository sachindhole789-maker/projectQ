using App.Domain.Common;

namespace App.Application.Common.Responses;

/// <summary>API-facing paginated payload.</summary>
public sealed record PaginatedResult<T>(IReadOnlyList<T> Items, PaginationMeta Pagination);

public static class PaginationExtensions
{
    public static PaginatedResult<TResponse> ToPaginatedResult<TSource, TResponse>(
        this PagedResult<TSource> source,
        Func<TSource, TResponse> map) =>
        new(
            source.Items.Select(map).ToList(),
            PaginationMeta.Create(source.TotalRecords, source.PageNumber, source.PageSize));
}
