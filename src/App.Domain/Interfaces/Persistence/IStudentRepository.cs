using App.Domain.Common;
using App.Domain.Dtos.Students;
using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface IStudentRepository
{
    Task<StudentDto?> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<PagedResult<StudentDto>> GetPagedAsync(StudentFilterDto filter, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(Student student, CancellationToken cancellationToken = default);
}
