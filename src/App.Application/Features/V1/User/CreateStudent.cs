using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace App.Application.Features.V1.User;

public sealed record CreateStudentCommand(
    string CustomerId,
    string Name,
    string Email,
    string Mobile,
    string City) : ICommand<CreateStudentResponse>;

public sealed record CreateStudentResponse(Guid StudentId, string CustomerId);

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Mobile).NotEmpty().Matches(@"^\+?[0-9]{7,15}$")
            .WithMessage("Mobile must contain 7-15 digits, optionally prefixed with +.");
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateStudentCommandHandler(
    ILogger<CreateStudentCommandHandler> logger,
    IStudentRepository studentRepository,
    IUserInfoProvider userInfo,
    TimeProvider timeProvider) : ICommandHandler<CreateStudentCommand, CreateStudentResponse>
{
    public async Task<Result<CreateStudentResponse>> Handle(
        CreateStudentCommand command,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<CreateStudentResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var existing = await studentRepository.GetByCustomerIdAsync(command.CustomerId, cancellationToken);
        if (existing is not null)
        {
            return Result<CreateStudentResponse>.Failure(
                AppStatusCodes.Conflict,
                "A student with this CustomerId already exists.");
        }

        var student = new Student
        {
            StudentId = Guid.NewGuid(),
            CustomerId = command.CustomerId.Trim(),
            Name = command.Name.Trim(),
            Email = command.Email.Trim().ToLowerInvariant(),
            Mobile = command.Mobile.Trim(),
            City = command.City.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            IsDeleted = false,
        };

        var id = await studentRepository.CreateAsync(student, cancellationToken);
        logger.LogInformation("Created student {StudentId} for customer {CustomerId}", id, student.CustomerId);

        return Result<CreateStudentResponse>.Success(
            new CreateStudentResponse(id, student.CustomerId),
            "Student created successfully");
    }
}
