using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Services;
using SmartFileImport.Api.Workers;

namespace SmartFileImport.Api.Tests;

public sealed class FileImportWorkerTests : IDisposable
{
    private readonly string _testDirectory;

    public FileImportWorkerTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenSupportedFilesExist_SendsThemToImportService()
    {
        var incomingFolder = CreateIncomingFolder();
        var csvPath = WriteFile(incomingFolder, "employees.csv");
        var excelPath = WriteFile(incomingFolder, "employees.xlsx");
        var textPath = WriteFile(incomingFolder, "notes.txt");
        var processedFolder = GetProcessedFolder();
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();

        Assert.Equal(
            new[] { "employees.csv", "employees.xlsx" },
            importService.ImportedFileNames);
        Assert.False(File.Exists(csvPath));
        Assert.False(File.Exists(excelPath));
        Assert.True(File.Exists(textPath));
        Assert.True(File.Exists(Path.Combine(processedFolder, "employees.csv")));
        Assert.True(File.Exists(Path.Combine(processedFolder, "employees.xlsx")));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenImportFails_ContinuesWithRemainingFiles()
    {
        var incomingFolder = CreateIncomingFolder();
        var firstPath = WriteFile(incomingFolder, "first.csv");
        var secondPath = WriteFile(incomingFolder, "second.csv");
        var processedFolder = GetProcessedFolder();
        var errorFolder = GetErrorFolder();
        var logger = new TestLogger<FileImportWorker>();
        var importService = new FakeFileImportService
        {
            FileNameToThrow = "first.csv"
        };
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder, logger: logger);

        await worker.ProcessPendingFilesAsync();

        Assert.Equal(
            new[] { "first.csv", "second.csv" },
            importService.ImportedFileNames);
        Assert.False(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));
        Assert.True(File.Exists(Path.Combine(errorFolder, "first.csv")));
        Assert.True(File.Exists(Path.Combine(processedFolder, "second.csv")));
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Exception is InvalidOperationException
                && entry.Message.Contains("Failed to process file 'first.csv'"));
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Message.Contains("Detected file 'second.csv' for import."));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenFileWasMoved_DoesNotImportItTwice()
    {
        var incomingFolder = CreateIncomingFolder();
        WriteFile(incomingFolder, "employees.csv");
        var processedFolder = GetProcessedFolder();
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();
        await worker.ProcessPendingFilesAsync();

        Assert.Equal(new[] { "employees.csv" }, importService.ImportedFileNames);
        Assert.True(File.Exists(Path.Combine(processedFolder, "employees.csv")));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenValidationFails_MovesFileToErrorFolder()
    {
        var incomingFolder = CreateIncomingFolder();
        var filePath = WriteFile(incomingFolder, "invalid.csv");
        var errorFolder = GetErrorFolder();
        var importService = new FakeFileImportService();
        importService.ResultsByFileName["invalid.csv"] = FileImportResult.Failed(
            new[] { "Record 1: Name is required." });
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();

        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(Path.Combine(errorFolder, "invalid.csv")));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenDestinationFileExists_UsesUniqueFileName()
    {
        var incomingFolder = CreateIncomingFolder();
        WriteFile(incomingFolder, "employees.csv", "new file");
        var processedFolder = GetProcessedFolder();
        Directory.CreateDirectory(processedFolder);
        File.WriteAllText(Path.Combine(processedFolder, "employees.csv"), "existing file");
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();

        Assert.Equal("existing file", File.ReadAllText(Path.Combine(processedFolder, "employees.csv")));
        Assert.Equal("new file", File.ReadAllText(Path.Combine(processedFolder, "employees_1.csv")));
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenFileMoveFails_ContinuesSafely()
    {
        var incomingFolder = CreateIncomingFolder();
        var firstPath = WriteFile(incomingFolder, "first.csv");
        var secondPath = WriteFile(incomingFolder, "second.csv");
        var processedFolder = Path.Combine(_testDirectory, "ProcessedAsFile");
        File.WriteAllText(processedFolder, "not a folder");
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder, processedFolder);

        await worker.ProcessPendingFilesAsync();
        await worker.ProcessPendingFilesAsync();

        Assert.Equal(
            new[] { "first.csv", "second.csv" },
            importService.ImportedFileNames);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string CreateIncomingFolder()
    {
        var incomingFolder = Path.Combine(_testDirectory, "Incoming");
        Directory.CreateDirectory(incomingFolder);
        return incomingFolder;
    }

    private string GetProcessedFolder()
    {
        return Path.Combine(_testDirectory, "Processed");
    }

    private string GetErrorFolder()
    {
        return Path.Combine(_testDirectory, "Error");
    }

    private static ServiceProvider CreateServiceProvider(IFileImportService importService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => importService);
        return services.BuildServiceProvider();
    }

    private FileImportWorker CreateWorker(
        IServiceProvider serviceProvider,
        string incomingFolder,
        string? processedFolder = null,
        string? errorFolder = null,
        ILogger<FileImportWorker>? logger = null)
    {
        return new FileImportWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FileProcessingOptions
            {
                InputFolder = incomingFolder,
                ProcessedFolder = processedFolder ?? GetProcessedFolder(),
                ErrorFolder = errorFolder ?? GetErrorFolder(),
                ScanIntervalSeconds = 1
            }),
            logger ?? NullLogger<FileImportWorker>.Instance);
    }

    private static string WriteFile(string folder, string fileName, string contents = "test")
    {
        var filePath = Path.Combine(folder, fileName);
        File.WriteAllText(filePath, contents);
        return filePath;
    }

    private sealed class FakeFileImportService : IFileImportService
    {
        public List<string> ImportedFileNames { get; } = new();

        public Dictionary<string, FileImportResult> ResultsByFileName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? FileNameToThrow { get; init; }

        public Task<FileImportResult> ImportAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(filePath);
            ImportedFileNames.Add(fileName);

            if (string.Equals(fileName, FileNameToThrow, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Import failed.");
            }

            if (ResultsByFileName.TryGetValue(fileName, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(FileImportResult.Success(1));
        }
    }
}
