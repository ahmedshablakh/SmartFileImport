using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;
using SmartFileImport.Api.Services;
using SmartFileImport.Api.Workers;

namespace SmartFileImport.Api.Tests;

public sealed class FileImportWorkflowTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _incomingFolder;
    private readonly string _processedFolder;
    private readonly string _errorFolder;

    public FileImportWorkflowTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));
        _incomingFolder = Path.Combine(_testDirectory, "Incoming");
        _processedFolder = Path.Combine(_testDirectory, "Processed");
        _errorFolder = Path.Combine(_testDirectory, "Error");

        Directory.CreateDirectory(_incomingFolder);
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenCsvAndExcelAreValid_ImportsEmployeesMovesFilesRecordsHistoryAndLogs()
    {
        var csvPath = WriteCsv(
            "employees.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            Sara Example,sara@example.com,Finance,5200
            """);
        var excelPath = WriteWorkbook(
            "employees.xlsx",
            ("Mona Example", "mona@example.com", "Operations", 6100m));
        var serviceLogger = new TestLogger<FileImportService>();
        var workerLogger = new TestLogger<FileImportWorker>();
        using var serviceProvider = CreateServiceProvider(serviceLogger);
        var worker = CreateWorker(serviceProvider, workerLogger);

        await worker.ProcessPendingFilesAsync();

        var employees = await ReadEmployeesAsync(serviceProvider);
        var importHistory = await ReadImportHistoryAsync(serviceProvider);

        Assert.Equal(
            new[] { "ahmed@example.com", "mona@example.com", "sara@example.com" },
            employees.Select(employee => employee.Email).ToArray());
        Assert.False(File.Exists(csvPath));
        Assert.False(File.Exists(excelPath));
        Assert.True(File.Exists(Path.Combine(_processedFolder, "employees.csv")));
        Assert.True(File.Exists(Path.Combine(_processedFolder, "employees.xlsx")));
        Assert.Empty(Directory.EnumerateFiles(_incomingFolder));
        Assert.All(importHistory, import => Assert.Equal("Success", import.Status));
        Assert.Contains(
            importHistory,
            import => import.FileName == "employees.csv"
                && import.RecordCount == 2
                && import.ErrorMessage is null);
        Assert.Contains(
            importHistory,
            import => import.FileName == "employees.xlsx"
                && import.RecordCount == 1
                && import.ErrorMessage is null);
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Message.Contains("Successfully imported file 'employees.csv'"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Message.Contains("Moved file 'employees.xlsx' to processed folder"));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenCsvIsEmpty_MovesFileToErrorRecordsFailureAndLogsError()
    {
        var filePath = WriteCsv("empty.csv", string.Empty);
        var serviceLogger = new TestLogger<FileImportService>();
        var workerLogger = new TestLogger<FileImportWorker>();
        using var serviceProvider = CreateServiceProvider(serviceLogger);
        var worker = CreateWorker(serviceProvider, workerLogger);

        await worker.ProcessPendingFilesAsync();

        var employees = await ReadEmployeesAsync(serviceProvider);
        var importHistory = await ReadImportHistoryAsync(serviceProvider);
        var failedImport = Assert.Single(importHistory);

        Assert.Empty(employees);
        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(Path.Combine(_errorFolder, "empty.csv")));
        Assert.Equal("empty.csv", failedImport.FileName);
        Assert.Equal("Failed", failedImport.Status);
        Assert.Equal(0, failedImport.RecordCount);
        Assert.Contains("empty", failedImport.ErrorMessage);
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Exception is InvalidDataException
                && entry.Message.Contains("Import failed for file 'empty.csv'"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Message.Contains("Failed to process file 'empty.csv'"));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenCsvContainsInvalidData_MovesFileToErrorDoesNotInsertEmployeesRecordsFailureAndLogsWarning()
    {
        var filePath = WriteCsv(
            "invalid-data.csv",
            """
            Name,Email,Department,Salary
            ,not-an-email,,0
            """);
        var serviceLogger = new TestLogger<FileImportService>();
        var workerLogger = new TestLogger<FileImportWorker>();
        using var serviceProvider = CreateServiceProvider(serviceLogger);
        var worker = CreateWorker(serviceProvider, workerLogger);

        await worker.ProcessPendingFilesAsync();

        var employees = await ReadEmployeesAsync(serviceProvider);
        var importHistory = await ReadImportHistoryAsync(serviceProvider);
        var failedImport = Assert.Single(importHistory);

        Assert.Empty(employees);
        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(Path.Combine(_errorFolder, "invalid-data.csv")));
        Assert.Equal("invalid-data.csv", failedImport.FileName);
        Assert.Equal("Failed", failedImport.Status);
        Assert.Equal(0, failedImport.RecordCount);
        Assert.Contains("Record 1: Name is required.", failedImport.ErrorMessage);
        Assert.Contains("Record 1: Email must have a valid format.", failedImport.ErrorMessage);
        Assert.Contains("Record 1: Department is required.", failedImport.ErrorMessage);
        Assert.Contains("Record 1: Salary must be greater than zero.", failedImport.ErrorMessage);
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains("Validation failed for file 'invalid-data.csv'"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains("File 'invalid-data.csv' failed validation"));
    }

    [Fact]
    public async Task ImportAsync_WhenFileTypeIsUnsupported_RecordsFailureAndLogsError()
    {
        var filePath = WriteFile(_incomingFolder, "employees.txt", "not supported");
        var serviceLogger = new TestLogger<FileImportService>();
        using var serviceProvider = CreateServiceProvider(serviceLogger);

        using (var scope = serviceProvider.CreateScope())
        {
            var importService = scope.ServiceProvider.GetRequiredService<IFileImportService>();

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => importService.ImportAsync(filePath));

            Assert.Contains("unsupported file type", exception.Message);
        }

        var employees = await ReadEmployeesAsync(serviceProvider);
        var importHistory = await ReadImportHistoryAsync(serviceProvider);
        var failedImport = Assert.Single(importHistory);

        Assert.Empty(employees);
        Assert.Equal("employees.txt", failedImport.FileName);
        Assert.Equal("Failed", failedImport.Status);
        Assert.Equal(0, failedImport.RecordCount);
        Assert.Contains("unsupported file type", failedImport.ErrorMessage);
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Exception is InvalidDataException
                && entry.Message.Contains("Import failed for file 'employees.txt'"));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenDatabaseSaveFails_MovesFilesToErrorLogsErrorsAndContinues()
    {
        var firstPath = WriteCsv(
            "first.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            """);
        var secondPath = WriteCsv(
            "second.csv",
            """
            Name,Email,Department,Salary
            Sara Example,sara@example.com,Finance,5200
            """);
        var serviceLogger = new TestLogger<FileImportService>();
        var workerLogger = new TestLogger<FileImportWorker>();
        using var serviceProvider = CreateServiceProvider(
            serviceLogger,
            failDatabaseSave: true);
        var worker = CreateWorker(serviceProvider, workerLogger);

        await worker.ProcessPendingFilesAsync();

        Assert.False(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));
        Assert.True(File.Exists(Path.Combine(_errorFolder, "first.csv")));
        Assert.True(File.Exists(Path.Combine(_errorFolder, "second.csv")));
        Assert.False(Directory.Exists(_processedFolder));
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Exception is InvalidOperationException
                && entry.Message.Contains("Import failed for file 'first.csv'"));
        Assert.Contains(
            serviceLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Message.Contains("Failed to record import history for failed file 'first.csv'"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Message.Contains("Failed to process file 'first.csv'"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Message.Contains("Detected file 'second.csv' for import."));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenFileMoveFails_LogsErrorAndContinuesWithRemainingFiles()
    {
        var firstPath = WriteCsv(
            "first.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            """);
        var secondPath = WriteCsv(
            "second.csv",
            """
            Name,Email,Department,Salary
            Sara Example,sara@example.com,Finance,5200
            """);
        var processedPathAsFile = Path.Combine(_testDirectory, "ProcessedAsFile");
        File.WriteAllText(processedPathAsFile, "not a folder");
        var workerLogger = new TestLogger<FileImportWorker>();
        using var serviceProvider = CreateServiceProvider();
        var worker = CreateWorker(
            serviceProvider,
            workerLogger,
            processedFolder: processedPathAsFile);

        await worker.ProcessPendingFilesAsync();

        var employees = await ReadEmployeesAsync(serviceProvider);
        var importHistory = await ReadImportHistoryAsync(serviceProvider);

        Assert.Equal(2, employees.Count);
        Assert.Equal(2, importHistory.Count);
        Assert.All(importHistory, import => Assert.Equal("Success", import.Status));
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.False(Directory.Exists(_processedFolder));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Message.Contains("Failed to move file 'first.csv' to processed folder"));
        Assert.Contains(
            workerLogger.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Message.Contains("Detected file 'second.csv' for import."));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private ServiceProvider CreateServiceProvider(
        TestLogger<FileImportService>? serviceLogger = null,
        bool failDatabaseSave = false)
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");

        if (failDatabaseSave)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            services.AddScoped<ApplicationDbContext>(_ => new FailingApplicationDbContext(options));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        }

        services.AddScoped<ICsvFileReader, CsvFileReader>();
        services.AddScoped<IExcelFileReader, ExcelFileReader>();
        services.AddScoped<IEmployeeValidator, EmployeeValidator>();
        services.AddScoped<IFileImportService, FileImportService>();
        ILogger<FileImportService> logger = serviceLogger is null
            ? NullLogger<FileImportService>.Instance
            : serviceLogger;
        services.AddSingleton(logger);

        return services.BuildServiceProvider();
    }

    private FileImportWorker CreateWorker(
        IServiceProvider serviceProvider,
        ILogger<FileImportWorker> workerLogger,
        string? processedFolder = null,
        string? errorFolder = null)
    {
        return new FileImportWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FileProcessingOptions
            {
                InputFolder = _incomingFolder,
                ProcessedFolder = processedFolder ?? _processedFolder,
                ErrorFolder = errorFolder ?? _errorFolder,
                ScanIntervalSeconds = 1
            }),
            workerLogger);
    }

    private static async Task<List<Employee>> ReadEmployeesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await dbContext.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.Email)
            .ToListAsync();
    }

    private static async Task<List<ImportHistory>> ReadImportHistoryAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await dbContext.ImportHistories
            .AsNoTracking()
            .OrderBy(importHistory => importHistory.FileName)
            .ToListAsync();
    }

    private string WriteCsv(string fileName, string contents)
    {
        return WriteFile(_incomingFolder, fileName, contents);
    }

    private string WriteWorkbook(
        string fileName,
        params (string Name, string Email, string Department, decimal Salary)[] rows)
    {
        var filePath = Path.Combine(_incomingFolder, fileName);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Employees");
        AddHeaders(worksheet);

        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2;

            worksheet.Cell(rowNumber, 1).Value = row.Name;
            worksheet.Cell(rowNumber, 2).Value = row.Email;
            worksheet.Cell(rowNumber, 3).Value = row.Department;
            worksheet.Cell(rowNumber, 4).Value = row.Salary;
        }

        workbook.SaveAs(filePath);

        return filePath;
    }

    private static void AddHeaders(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "Email";
        worksheet.Cell(1, 3).Value = "Department";
        worksheet.Cell(1, 4).Value = "Salary";
    }

    private static string WriteFile(string folder, string fileName, string contents)
    {
        var filePath = Path.Combine(folder, fileName);
        File.WriteAllText(filePath, contents);
        return filePath;
    }

    private sealed class FailingApplicationDbContext : ApplicationDbContext
    {
        public FailingApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromException<int>(
                new InvalidOperationException("Simulated database failure."));
        }
    }
}
