using App.Application.Abstractions;
using App.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace App.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRequestMetrics, RequestMetrics>();
        services.AddSingleton<IRealtimeDashboardService, InMemoryRealtimeDashboardService>();
        services.AddSingleton<IDocumentValidationService, DocumentValidationService>();
        services.AddScoped<INotificationService, NotificationService>();

        RegisterHandlers(services);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        var handlerDefinitions = new[]
        {
            typeof(IQueryHandler<,>),
            typeof(ICommandHandler<,>),
        };

        var registrations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(iface => iface.IsGenericType && handlerDefinitions.Contains(iface.GetGenericTypeDefinition()))
                .Select(iface => new { Interface = iface, Implementation = type }))
            .Distinct();

        foreach (var registration in registrations)
        {
            services.AddScoped(registration.Interface, registration.Implementation);
        }
    }
}
