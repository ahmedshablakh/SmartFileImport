using System.ComponentModel.DataAnnotations;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class EmployeeValidator : IEmployeeValidator
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public IReadOnlyList<string> Validate(IReadOnlyList<Employee> employees)
    {
        ArgumentNullException.ThrowIfNull(employees);

        var errors = new List<string>();

        for (var index = 0; index < employees.Count; index++)
        {
            ValidateEmployee(employees[index], index + 1, errors);
        }

        return errors;
    }

    private static void ValidateEmployee(Employee employee, int recordNumber, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(employee.Name))
        {
            errors.Add($"Record {recordNumber}: Name is required.");
        }

        if (!IsValidEmail(employee.Email))
        {
            errors.Add($"Record {recordNumber}: Email must have a valid format.");
        }

        if (string.IsNullOrWhiteSpace(employee.Department))
        {
            errors.Add($"Record {recordNumber}: Department is required.");
        }

        if (employee.Salary <= 0)
        {
            errors.Add($"Record {recordNumber}: Salary must be greater than zero.");
        }
    }

    private static bool IsValidEmail(string? email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && EmailAddressValidator.IsValid(email);
    }
}
