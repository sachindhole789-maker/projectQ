namespace App.Application.Common.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Base64 master key (32 bytes recommended). In Production supply it through
    /// the SECURITY__MASTERKEY environment variable or AWS Secrets Manager, never Git.
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;

    public int Pbkdf2Iterations { get; set; } = 210_000;
}
