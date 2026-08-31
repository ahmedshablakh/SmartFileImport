using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public interface IExcelFileReader
{
    IReadOnlyList<Employee> Read(string filePath);
}
