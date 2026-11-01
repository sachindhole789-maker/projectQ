namespace App.Application.Common.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>SqlServer | AwsRds | Oracle</summary>
    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>Runs embedded schema scripts and seed data. Development environments only.</summary>
    public bool InitializeOnStartup { get; set; }

    public bool SeedOnStartup { get; set; }
    public int AuditRetentionDays { get; set; } = 90;
}
