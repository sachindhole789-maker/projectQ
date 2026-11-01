using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;
using UserEntity = App.Domain.Entities.User;

namespace App.Application.Features.V1.Auth;

public sealed record RegisterUserCommand(string Email, string Password, string DisplayName)
    : ICommand<RegisterUserResponse>;

public sealed record RegisterUserResponse(Guid UserId, string Email);

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12).WithMessage("Password must be at least 12 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");
    }
}

public sealed class RegisterUserCommandHandler(
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await authRepository.EmailExistsAsync(email, cancellationToken))
        {
            return Result<RegisterUserResponse>.Failure(
                AppStatusCodes.Conflict,
                "An account with this email already exists.");
        }

        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(command.Password),
            DisplayName = command.DisplayName.Trim(),
            Roles = "User",
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        await authRepository.CreateUserAsync(user, cancellationToken);

        return Result<RegisterUserResponse>.Success(
            new RegisterUserResponse(user.UserId, user.Email),
            "User registered successfully");
    }
}
