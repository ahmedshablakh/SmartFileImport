using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class FileImportService : IFileImportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICsvFileReader _csvFileReader;
    private readonly IExcelFileReader _excelFileReader;
    private readonly IEmployeeValidator _employeeValidator;

    public FileImportService(
        ApplicationDbContext dbContext,
        ICsvFileReader csvFileReader,
        IExcelFileReader excelFileReader,
        IEmployeeValidator employeeValidator)
    {
        _dbContext = dbContext;
        _csvFileReader = csvFileReader;
        _excelFileReader = excelFileReader;
        _employeeValidator = employeeValidator;
    }

    public async Task<FileImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var employees = ReadEmployees(filePath);
        var validationErrors = _employeeValidator.Validate(employees);

        if (validationErrors.Count > 0)
        {
            return FileImportResult.Failed(validationErrors);
        }

        await _dbContext.Employees.AddRangeAsync(employees, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return FileImportResult.Success(employees.Count);
    }

    private IReadOnlyList<Employee> ReadEmployees(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return _csvFileReader.Read(filePath);
        }

        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return _excelFileReader.Read(filePath);
        }

        var displayExtension = string.IsNullOrWhiteSpace(extension)
            ? "no extension"
            : extension;

        throw new InvalidDataException(
            $"File '{Path.GetFileName(filePath)}' uses unsupported file type '{displayExtension}'. Supported file types are .csv and .xlsx.");
    }
}
