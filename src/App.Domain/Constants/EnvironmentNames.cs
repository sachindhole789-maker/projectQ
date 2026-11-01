namespace App.Domain.Constants;

/// <summary>
/// The four environments this template supports. Set
/// ASPNETCORE_ENVIRONMENT (or DOTNET_ENVIRONMENT for the runner) to one of these.
/// </summary>
public static class EnvironmentNames
{
    public const string PreDevelopment = "PreDevelopment";
    public const string Development = "Development";
    public const string Testing = "Testing";
    public const string Production = "Production";

    public static readonly string[] All =
    [
        PreDevelopment,
        Development,
        Testing,
        Production,
    ];
}
