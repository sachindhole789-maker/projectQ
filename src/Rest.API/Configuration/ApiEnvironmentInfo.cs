using App.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Rest.API.Configuration;

/// <summary>Exposes the ASP.NET Core environment name to Application code.</summary>
public sealed class ApiEnvironmentInfo(IHostEnvironment environment) : IEnvironmentInfo
{
    public string Name => environment.EnvironmentName;

    public bool IsProduction => environment.IsProduction();
}
