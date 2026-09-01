using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public interface IEmployeeValidator
{
    IReadOnlyList<string> Validate(IReadOnlyList<Employee> employees);
}
