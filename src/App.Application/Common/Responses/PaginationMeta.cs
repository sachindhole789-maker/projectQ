namespace App.Application.Common.Responses;

public sealed record PaginationMeta
{
    public int TotalRecords { get; init; }
    public int PageSize { get; init; }
    public int PageNumber { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;

    public static PaginationMeta Create(int totalRecords, int pageNumber, int pageSize) => new()
    {
        TotalRecords = totalRecords,
        PageNumber = pageNumber,
        PageSize = pageSize,
    };
}
