using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class FileImportService : IFileImportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICsvFileReader _csvFileReader;
    private readonly IExcelFileReader _excelFileReader;
    private readonly IEmployeeValidator _employeeValidator;
    private readonly ILogger<FileImportService> _logger;

    public FileImportService(
        ApplicationDbContext dbContext,
        ICsvFileReader csvFileReader,
        IExcelFileReader excelFileReader,
        IEmployeeValidator employeeValidator,
        ILogger<FileImportService> logger)
    {
        _dbContext = dbContext;
        _csvFileReader = csvFileReader;
        _excelFileReader = excelFileReader;
        _employeeValidator = employeeValidator;
        _logger = logger;
    }

    public async Task<FileImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var fileName = Path.GetFileName(filePath);

        try
        {
            _logger.LogInformation("Starting import for file '{FileName}'.", fileName);

            var employees = ReadEmployees(filePath);

            _logger.LogInformation(
                "Read {RecordCount} record(s) from file '{FileName}'.",
                employees.Count,
                fileName);

            var validationErrors = _employeeValidator.Validate(employees);

            if (validationErrors.Count > 0)
            {
                _logger.LogWarning(
                    "Validation failed for file '{FileName}' with {ErrorCount} error(s): {ValidationErrors}",
                    fileName,
                    validationErrors.Count,
                    string.Join("; ", validationErrors));

                return FileImportResult.Failed(validationErrors);
            }

            _logger.LogInformation(
                "Saving {RecordCount} employee record(s) from file '{FileName}'.",
                employees.Count,
                fileName);

            await _dbContext.Employees.AddRangeAsync(employees, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully imported file '{FileName}' with {RecordCount} record(s).",
                fileName,
                employees.Count);

            return FileImportResult.Success(employees.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Import for file '{FileName}' was canceled.", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for file '{FileName}'.", fileName);
            throw;
        }
    }

    private IReadOnlyList<Employee> ReadEmployees(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Detected CSV file '{FileName}'.", Path.GetFileName(filePath));
            return _csvFileReader.Read(filePath);
        }

        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Detected Excel file '{FileName}'.", Path.GetFileName(filePath));
            return _excelFileReader.Read(filePath);
        }

        var displayExtension = string.IsNullOrWhiteSpace(extension)
            ? "no extension"
            : extension;

        throw new InvalidDataException(
            $"File '{Path.GetFileName(filePath)}' uses unsupported file type '{displayExtension}'. Supported file types are .csv and .xlsx.");
    }
}
