using Microsoft.Extensions.DependencyInjection;
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
        WriteFile(incomingFolder, "employees.csv");
        WriteFile(incomingFolder, "employees.xlsx");
        WriteFile(incomingFolder, "notes.txt");
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();

        Assert.Equal(
            new[] { "employees.csv", "employees.xlsx" },
            importService.ImportedFileNames);
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenImportFails_ContinuesWithRemainingFiles()
    {
        var incomingFolder = CreateIncomingFolder();
        WriteFile(incomingFolder, "first.csv");
        WriteFile(incomingFolder, "second.csv");
        var importService = new FakeFileImportService
        {
            FileNameToThrow = "first.csv"
        };
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();

        Assert.Equal(
            new[] { "first.csv", "second.csv" },
            importService.ImportedFileNames);
    }

    [Fact]
    public async Task ProcessPendingFilesAsync_WhenFileIsUnchanged_DoesNotImportItTwice()
    {
        var incomingFolder = CreateIncomingFolder();
        WriteFile(incomingFolder, "employees.csv");
        var importService = new FakeFileImportService();
        using var serviceProvider = CreateServiceProvider(importService);
        var worker = CreateWorker(serviceProvider, incomingFolder);

        await worker.ProcessPendingFilesAsync();
        await worker.ProcessPendingFilesAsync();

        Assert.Equal(new[] { "employees.csv" }, importService.ImportedFileNames);
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

    private static ServiceProvider CreateServiceProvider(IFileImportService importService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => importService);
        return services.BuildServiceProvider();
    }

    private static FileImportWorker CreateWorker(IServiceProvider serviceProvider, string incomingFolder)
    {
        return new FileImportWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FileProcessingOptions
            {
                InputFolder = incomingFolder,
                ScanIntervalSeconds = 1
            }),
            NullLogger<FileImportWorker>.Instance);
    }

    private static void WriteFile(string folder, string fileName)
    {
        File.WriteAllText(Path.Combine(folder, fileName), "test");
    }

    private sealed class FakeFileImportService : IFileImportService
    {
        public List<string> ImportedFileNames { get; } = new();

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

            return Task.FromResult(FileImportResult.Success(1));
        }
    }
}
