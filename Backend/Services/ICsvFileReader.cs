using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public interface ICsvFileReader
{
    IReadOnlyList<Employee> Read(string filePath);
}
