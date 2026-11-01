using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using App.Application.Abstractions;
using App.Application.Common.Responses;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public sealed class Dispatcher(
    IServiceProvider serviceProvider,
    IRequestContext requestContext,
    ILogger<Dispatcher> logger) : IDispatcher
{
    public Task<Result<TResponse>> ExecuteQuery<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<TResponse>(typeof(IQueryHandler<,>), query, cancellationToken);

    public Task<Result<TResponse>> ExecuteCommand<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<TResponse>(typeof(ICommandHandler<,>), command, cancellationToken);

    private async Task<Result<TResponse>> ExecuteAsync<TResponse>(
        Type openHandlerType,
        object request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var stopwatch = Stopwatch.StartNew();

        var validationMessage = await ValidateAsync(request, requestType, cancellationToken);
        if (validationMessage is not null)
        {
            return AttachRequestId(Result<TResponse>.Failure(AppStatusCodes.ValidationError, validationMessage));
        }

        var handlerType = openHandlerType.MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);
        var handlerMethod = handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"Handler {handlerType.Name} has no Handle method.");

        try
        {
            var task = (Task<Result<TResponse>>)handlerMethod.Invoke(handler, [request, cancellationToken])!;
            var result = await task;

            logger.LogInformation(
                "Handled {RequestType} in {ElapsedMs} ms with status {StatusCode}",
                requestType.Name,
                stopwatch.ElapsedMilliseconds,
                result.StatusCode);

            return AttachRequestId(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            logger.LogError(ex.InnerException, "Handler {RequestType} failed", requestType.Name);
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<string?> ValidateAsync(object request, Type requestType, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        if (serviceProvider.GetService(validatorType) is not IValidator validator)
        {
            return null;
        }

        var contextType = typeof(ValidationContext<>).MakeGenericType(requestType);
        var context = (IValidationContext)Activator.CreateInstance(contextType, request)!;
        ValidationResult validation = await validator.ValidateAsync(context, cancellationToken);

        if (validation.IsValid)
        {
            return null;
        }

        return string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage).Distinct());
    }

    private Result<TResponse> AttachRequestId<TResponse>(Result<TResponse> result) =>
        result with { RequestId = requestContext.RequestId };
}
