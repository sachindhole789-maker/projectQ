using App.Domain.Common;
using App.Domain.Dtos.Students;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;

namespace App.Infrastructure.Persistence.Repositories;

public sealed class StudentRepository(IDbManager dbManager) : IStudentRepository
{
    public Task<StudentDto?> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<StudentDto>(
            "sp_Student_GetByCustomerId",
            new { CustomerId = customerId },
            cancellationToken);

    public Task<PagedResult<StudentDto>> GetPagedAsync(
        StudentFilterDto filter,
        CancellationToken cancellationToken = default) =>
        dbManager.QueryPagedAsync<StudentDto>(
            "sp_Student_GetPaged",
            new
            {
                filter.Name,
                filter.Email,
                filter.City,
            },
            filter.PageNumber,
            filter.PageSize,
            cancellationToken: cancellationToken);

    public async Task<Guid> CreateAsync(Student student, CancellationToken cancellationToken = default)
    {
        await dbManager.ExecuteAsync("sp_Student_Create", new
        {
            student.StudentId,
            student.CustomerId,
            student.Name,
            student.Email,
            student.Mobile,
            student.City,
            student.CreatedAtUtc,
        }, cancellationToken);

        return student.StudentId;
    }
}
