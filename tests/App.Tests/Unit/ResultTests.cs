using App.Application.Common.Responses;

namespace App.Tests.Unit;

public sealed class ResultTests
{
    [Fact]
    public void Success_sets_ok_status_and_payload()
    {
        var result = Result<string>.Success("value", "done");

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Data);
        Assert.Equal("200", result.StatusCode);
        Assert.Equal(ApiStatus.Success, result.Status);
        Assert.Equal("done", result.Message);
    }

    [Fact]
    public void Failure_exposes_empty_collection_for_list_payloads()
    {
        var result = Result<IReadOnlyList<int>>.Failure(AppStatusCodes.NotFound, "missing");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!);
        Assert.Equal("404", result.StatusCode);
        Assert.Equal(ApiStatus.Failure, result.Status);
    }

    [Fact]
    public void Paginated_success_carries_pagination_metadata()
    {
        var pagination = PaginationMeta.Create(totalRecords: 41, pageNumber: 2, pageSize: 20);

        var result = Result<IReadOnlyList<int>>.SuccessPaginated([1, 2, 3], pagination);

        Assert.Equal(41, result.Pagination!.TotalRecords);
        Assert.Equal(3, result.Pagination.TotalPages);
        Assert.True(result.Pagination.HasNextPage);
        Assert.True(result.Pagination.HasPreviousPage);
    }

    [Fact]
    public void Two_grid_success_keeps_second_payload_and_pagination()
    {
        var first = PaginationMeta.Create(10, 1, 10);
        var second = PaginationMeta.Create(5, 1, 10);

        var result = Result<string>.SuccessPaginated("rows", first, new[] { "extra" }, second);

        Assert.Equal("rows", result.Data);
        Assert.NotNull(result.Data2);
        Assert.Equal(10, result.Pagination!.TotalRecords);
        Assert.Equal(5, result.Pagination2!.TotalRecords);
    }

    [Fact]
    public void ToApiResponse_preserves_the_common_envelope()
    {
        var result = Result<int>.Success(7) with { RequestId = "req-1" };

        var response = result.ToApiResponse();

        Assert.Equal("req-1", response.RequestId);
        Assert.Equal(7, response.Data);
        Assert.Equal("200", response.StatusCode);
        Assert.Equal(ApiStatus.Success, response.Status);
    }
}
