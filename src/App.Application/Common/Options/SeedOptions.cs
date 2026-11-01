namespace App.Application.Common.Options;

/// <summary>
/// Optional development seed. Supply the password through environment variables
/// (SEED__ADMINPASSWORD) or user-secrets; never commit it.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@example.com";
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = "Administrator";
}
