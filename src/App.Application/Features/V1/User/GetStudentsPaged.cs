using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Common;
using App.Domain.Dtos.Students;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;

namespace App.Application.Features.V1.User;

public sealed record GetStudentsPagedQuery(
    string? Name,
    string? Email,
    string? City,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<GetStudentsPagedResponse>;

public sealed record StudentListItem(
    Guid StudentId,
    string CustomerId,
    string Name,
    string Email,
    string Mobile,
    string City);

public sealed record GetStudentsPagedResponse(IReadOnlyList<StudentListItem> Items);

public sealed class GetStudentsPagedQueryValidator : AbstractValidator<GetStudentsPagedQuery>
{
    public GetStudentsPagedQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class GetStudentsPagedQueryHandler(
    IStudentRepository studentRepository,
    IUserInfoProvider userInfo) : IQueryHandler<GetStudentsPagedQuery, GetStudentsPagedResponse>
{
    public async Task<Result<GetStudentsPagedResponse>> Handle(
        GetStudentsPagedQuery query,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<GetStudentsPagedResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var filter = new StudentFilterDto
        {
            Name = query.Name,
            Email = query.Email,
            City = query.City,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
        };

        PagedResult<StudentDto> paged = await studentRepository.GetPagedAsync(filter, cancellationToken);
        var paginated = paged.ToPaginatedResult(student => new StudentListItem(
            student.StudentId,
            student.CustomerId,
            student.Name,
            student.Email,
            student.Mobile,
            student.City));

        return Result<GetStudentsPagedResponse>.SuccessPaginated(
            new GetStudentsPagedResponse(paginated.Items),
            paginated.Pagination,
            "Data retrieved successfully");
    }
}
