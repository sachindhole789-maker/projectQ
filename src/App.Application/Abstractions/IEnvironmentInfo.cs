namespace App.Application.Abstractions;

public interface IEnvironmentInfo
{
    string Name { get; }
    bool IsProduction { get; }
}
