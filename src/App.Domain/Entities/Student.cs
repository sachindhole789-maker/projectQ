namespace App.Domain.Entities;

public sealed class Student
{
    public Guid StudentId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}
