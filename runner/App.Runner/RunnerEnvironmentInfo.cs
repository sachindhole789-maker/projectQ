using App.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace App.Runner;

public sealed class RunnerEnvironmentInfo(IHostEnvironment environment) : IEnvironmentInfo
{
    public string Name => environment.EnvironmentName;

    public bool IsProduction => environment.IsProduction();
}
