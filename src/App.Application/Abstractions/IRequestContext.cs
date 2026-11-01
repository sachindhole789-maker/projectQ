namespace App.Application.Abstractions;

/// <summary>Ambient HTTP request data used for responses and auditing.</summary>
public interface IRequestContext
{
    string RequestId { get; }
    string? CorrelationId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
