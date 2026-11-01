namespace App.Application.Common.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 25;
    public int LockMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
}
