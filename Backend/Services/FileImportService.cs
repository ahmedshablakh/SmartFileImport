using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class FileImportService : IFileImportService
{
    private const string SuccessStatus = "Success";
    private const string FailedStatus = "Failed";
    private const int MaxErrorMessageLength = 2000;

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
                var errorMessage = BuildErrorMessage(validationErrors);

                _logger.LogWarning(
                    "Validation failed for file '{FileName}' with {ErrorCount} error(s): {ValidationErrors}",
                    fileName,
                    validationErrors.Count,
                    errorMessage);

                await RecordFailedImportAsync(fileName, errorMessage, cancellationToken);

                return FileImportResult.Failed(validationErrors);
            }

            _logger.LogInformation(
                "Saving {RecordCount} employee record(s) from file '{FileName}'.",
                employees.Count,
                fileName);

            await _dbContext.Employees.AddRangeAsync(employees, cancellationToken);
            await _dbContext.ImportHistories.AddAsync(
                CreateSuccessHistory(fileName, employees.Count),
                cancellationToken);
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
            await TryRecordFailedImportAsync(fileName, ex.Message, cancellationToken);
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

    private static ImportHistory CreateSuccessHistory(string fileName, int recordCount)
    {
        return new ImportHistory
        {
            FileName = fileName,
            Status = SuccessStatus,
            RecordCount = recordCount,
            ProcessedAt = DateTime.UtcNow
        };
    }

    private static ImportHistory CreateFailedHistory(string fileName, string errorMessage)
    {
        return new ImportHistory
        {
            FileName = fileName,
            Status = FailedStatus,
            RecordCount = 0,
            ProcessedAt = DateTime.UtcNow,
            ErrorMessage = TrimErrorMessage(errorMessage)
        };
    }

    private async Task RecordFailedImportAsync(
        string fileName,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await _dbContext.ImportHistories.AddAsync(
            CreateFailedHistory(fileName, errorMessage),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TryRecordFailedImportAsync(
        string fileName,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.ChangeTracker.Clear();
            await RecordFailedImportAsync(fileName, errorMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception historyException)
        {
            _logger.LogError(
                historyException,
                "Failed to record import history for failed file '{FileName}'.",
                fileName);
        }
    }

    private static string BuildErrorMessage(IEnumerable<string> errors)
    {
        return TrimErrorMessage(string.Join("; ", errors));
    }

    private static string TrimErrorMessage(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "Import failed.";
        }

        return errorMessage.Length <= MaxErrorMessageLength
            ? errorMessage
            : errorMessage[..MaxErrorMessageLength];
    }
}
