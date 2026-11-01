using System.Collections.Concurrent;
using App.Application.Abstractions;

namespace App.Application.Services;

/// <summary>Thread-safe, allocation-light counters for the realtime dashboard.</summary>
public sealed class RequestMetrics : IRequestMetrics
{
    private readonly ConcurrentQueue<Sample> _samples = new();
    private long _totalRequests;
    private long _failedRequests;
    private long _totalDurationMs;

    public void Record(string path, int statusCode, long durationMs)
    {
        Interlocked.Increment(ref _totalRequests);
        if (statusCode >= 400)
        {
            Interlocked.Increment(ref _failedRequests);
        }

        Interlocked.Add(ref _totalDurationMs, durationMs);
        _samples.Enqueue(new Sample(DateTime.UtcNow, durationMs));
        Trim();
    }

    public RequestMetricsSnapshot Snapshot()
    {
        Trim();
        var total = Interlocked.Read(ref _totalRequests);
        var totalDuration = Interlocked.Read(ref _totalDurationMs);

        return new RequestMetricsSnapshot(
            total,
            Interlocked.Read(ref _failedRequests),
            _samples.Count,
            total == 0 ? 0 : Math.Round(totalDuration / (double)total, 2),
            DateTime.UtcNow);
    }

    private void Trim()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        while (_samples.TryPeek(out var sample) && sample.TimestampUtc < cutoff)
        {
            _samples.TryDequeue(out _);
        }
    }

    private readonly record struct Sample(DateTime TimestampUtc, long DurationMs);
}
