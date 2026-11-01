namespace App.Domain.Dtos.Students;

public sealed class StudentFilterDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
