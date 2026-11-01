namespace App.Application.Common.Options;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool RunAuditWriter { get; set; } = true;
    public bool RunNotificationDispatcher { get; set; }
    public bool RunAuditRetention { get; set; }
    public bool RunDashboardBroadcast { get; set; } = true;
    public int DashboardIntervalSeconds { get; set; } = 5;
    public int RetentionSweepHours { get; set; } = 24;
}
