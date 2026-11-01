namespace App.Domain.Common;

/// <summary>
/// Repository-level pagination envelope. The API layer maps this to its own
/// paginated response so domain types never leak out of the application boundary.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalRecords,
    int PageNumber,
    int PageSize)
{
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 20) =>
        new(Array.Empty<T>(), 0, pageNumber, pageSize);
}
