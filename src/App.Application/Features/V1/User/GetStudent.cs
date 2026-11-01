using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace App.Application.Features.V1.User;

public sealed record GetStudentQuery(string CustomerId) : IQuery<GetStudentResponse>;

public sealed record GetStudentResponse(string Name, string Email, string Mobile, string City);

public sealed class GetStudentQueryValidator : AbstractValidator<GetStudentQuery>
{
    public GetStudentQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Invalid CustomerId")
            .NotNull().WithMessage("Invalid CustomerId")
            .MaximumLength(50).WithMessage("CustomerId cannot exceed 50 characters")
            .WithName("CustomerId");
    }
}

public sealed class GetStudentQueryHandler(
    ILogger<GetStudentQueryHandler> logger,
    IStudentRepository studentRepository,
    IUserInfoProvider userInfo) : IQueryHandler<GetStudentQuery, GetStudentResponse>
{
    public async Task<Result<GetStudentResponse>> Handle(
        GetStudentQuery query,
        CancellationToken cancellationToken)
    {
        var userId = userInfo.UserId;
        logger.LogInformation("Handling GetStudentQuery for customer {CustomerId} by user {UserId}", query.CustomerId, userId);

        if (userId is null || userId == Guid.Empty)
        {
            return Result<GetStudentResponse>.Failure(AppStatusCodes.NotFound, "UserNotFound");
        }

        var student = await studentRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken);
        if (student is null)
        {
            return Result<GetStudentResponse>.Failure(AppStatusCodes.NotFound, "StudentNotFound");
        }

        var response = new GetStudentResponse(student.Name, student.Email, student.Mobile, student.City);
        return Result<GetStudentResponse>.Success(response, "Retrieved successfully");
    }
}
