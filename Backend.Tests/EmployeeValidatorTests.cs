using SmartFileImport.Api.Models;
using SmartFileImport.Api.Services;

namespace SmartFileImport.Api.Tests;

public class EmployeeValidatorTests
{
    private readonly EmployeeValidator _validator = new();

    [Fact]
    public void Validate_WhenEmployeeIsValid_ReturnsNoErrors()
    {
        var employees = new[]
        {
            new Employee
            {
                Name = "Ahmed Shablakh",
                Email = "ahmed@example.com",
                Department = "Engineering",
                Salary = 4500.50m
            }
        };

        var errors = _validator.Validate(employees);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenRequiredFieldsAreMissing_ReturnsClearErrors()
    {
        var employees = new[]
        {
            new Employee
            {
                Name = " ",
                Email = "ahmed@example.com",
                Department = "",
                Salary = 4500.50m
            }
        };

        var errors = _validator.Validate(employees);

        Assert.Contains("Record 1: Name is required.", errors);
        Assert.Contains("Record 1: Department is required.", errors);
    }

    [Fact]
    public void Validate_WhenEmailIsInvalid_ReturnsClearError()
    {
        var employees = new[]
        {
            new Employee
            {
                Name = "Ahmed Shablakh",
                Email = "not-an-email",
                Department = "Engineering",
                Salary = 4500.50m
            }
        };

        var errors = _validator.Validate(employees);

        Assert.Contains("Record 1: Email must have a valid format.", errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_WhenSalaryIsNotGreaterThanZero_ReturnsClearError(decimal salary)
    {
        var employees = new[]
        {
            new Employee
            {
                Name = "Ahmed Shablakh",
                Email = "ahmed@example.com",
                Department = "Engineering",
                Salary = salary
            }
        };

        var errors = _validator.Validate(employees);

        Assert.Contains("Record 1: Salary must be greater than zero.", errors);
    }

    [Fact]
    public void Validate_WhenMultipleEmployeesAreInvalid_IncludesRecordNumbers()
    {
        var employees = new[]
        {
            new Employee
            {
                Name = "",
                Email = "first@example.com",
                Department = "Engineering",
                Salary = 4500.50m
            },
            new Employee
            {
                Name = "Sara Example",
                Email = "invalid-email",
                Department = "Finance",
                Salary = 5200m
            }
        };

        var errors = _validator.Validate(employees);

        Assert.Contains("Record 1: Name is required.", errors);
        Assert.Contains("Record 2: Email must have a valid format.", errors);
    }
}
