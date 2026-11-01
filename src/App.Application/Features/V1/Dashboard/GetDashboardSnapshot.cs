using App.Application.Abstractions;
using App.Application.Common.Responses;

namespace App.Application.Features.V1.Dashboard;

public sealed record GetDashboardSnapshotQuery : IQuery<DashboardSnapshotResponse>;

public sealed record DashboardSnapshotResponse(
    RequestMetricsSnapshot Metrics,
    DashboardUpdate? LatestUpdate);

public sealed class GetDashboardSnapshotQueryHandler(
    IRequestMetrics requestMetrics,
    IRealtimeDashboardService dashboard) : IQueryHandler<GetDashboardSnapshotQuery, DashboardSnapshotResponse>
{
    public Task<Result<DashboardSnapshotResponse>> Handle(
        GetDashboardSnapshotQuery query,
        CancellationToken cancellationToken)
    {
        var response = new DashboardSnapshotResponse(requestMetrics.Snapshot(), dashboard.GetLatest());
        return Task.FromResult(Result<DashboardSnapshotResponse>.Success(response, "Snapshot retrieved"));
    }
}
