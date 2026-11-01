namespace App.Application.Common.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Flutter web/desktop debug servers use random localhost ports, so localhost
    /// is allowed on any port outside Production. Never enable this in Production.
    /// </summary>
    public bool AllowLocalhostAnyPort { get; set; } = true;

    public bool AllowCredentials { get; set; } = true;
}
