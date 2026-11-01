namespace App.Application.Abstractions;

public sealed record RequestMetricsSnapshot(
    long TotalRequests,
    long FailedRequests,
    long RequestsLastMinute,
    double AverageDurationMs,
    DateTime CapturedAtUtc);

public interface IRequestMetrics
{
    void Record(string path, int statusCode, long durationMs);

    RequestMetricsSnapshot Snapshot();
}
