namespace App.Application.Common.Options;

public sealed class VirusScanOptions
{
    public const string SectionName = "VirusScan";

    /// <summary>None | ClamAV</summary>
    public string Provider { get; set; } = "None";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 60;
}
