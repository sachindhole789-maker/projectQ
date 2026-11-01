using App.Application.Features.V1.Auth;
using App.Application.Features.V1.User;

namespace App.Tests.Unit;

public sealed class ValidationTests
{
    [Fact]
    public void GetStudent_rejects_empty_customer_id()
    {
        var result = new GetStudentQueryValidator().Validate(new GetStudentQuery(string.Empty));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetStudentsPaged_rejects_out_of_range_page_size()
    {
        var result = new GetStudentsPagedQueryValidator()
            .Validate(new GetStudentsPagedQuery(null, null, null, PageNumber: 0, PageSize: 500));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "PageNumber");
        Assert.Contains(result.Errors, error => error.PropertyName == "PageSize");
    }

    [Fact]
    public void CreateStudent_accepts_a_valid_payload()
    {
        var result = new CreateStudentCommandValidator().Validate(
            new CreateStudentCommand("CUST-1", "Ada Lovelace", "ada@example.com", "+919876543210", "Pune"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbolsHere123")]
    public void RegisterUser_rejects_weak_passwords(string password)
    {
        var result = new RegisterUserCommandValidator().Validate(
            new RegisterUserCommand("ada@example.com", password, "Ada"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterUser_accepts_a_strong_password()
    {
        var result = new RegisterUserCommandValidator().Validate(
            new RegisterUserCommand("ada@example.com", "Str0ng!Password123", "Ada"));

        Assert.True(result.IsValid);
    }
}
