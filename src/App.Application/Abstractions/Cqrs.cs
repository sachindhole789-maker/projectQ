using App.Application.Common.Responses;

namespace App.Application.Abstractions;

public interface IQuery<TResponse>;

public interface ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Custom CQRS dispatcher. Validates the request with FluentValidation, resolves
/// the matching handler from DI and returns a uniform <see cref="Result{T}"/>.
/// </summary>
public interface IDispatcher
{
    Task<Result<TResponse>> ExecuteQuery<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> ExecuteCommand<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);
}
