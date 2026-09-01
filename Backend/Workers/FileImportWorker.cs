using Microsoft.Extensions.Options;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Services;

namespace SmartFileImport.Api.Workers;

public class FileImportWorker : BackgroundService
{
    private static readonly string[] SupportedExtensions =
    {
        ".csv",
        ".xlsx"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FileProcessingOptions _options;
    private readonly ILogger<FileImportWorker> _logger;
    private readonly Dictionary<string, DateTime> _attemptedFileWriteTimes = new(StringComparer.OrdinalIgnoreCase);

    public FileImportWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FileProcessingOptions> options,
        ILogger<FileImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessPendingFilesAsync(CancellationToken cancellationToken = default)
    {
        var incomingFolder = ResolveIncomingFolder();
        Directory.CreateDirectory(incomingFolder);

        var files = Directory.EnumerateFiles(incomingFolder)
            .Where(IsSupportedFile)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryMarkFileForProcessing(filePath))
            {
                continue;
            }

            await ProcessFileAsync(filePath, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingFilesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while scanning the incoming folder.");
            }

            try
            {
                await Task.Delay(GetScanInterval(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<IFileImportService>();

            var result = await importService.ImportAsync(filePath, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Imported file '{FileName}' with {RecordCount} record(s).",
                    Path.GetFileName(filePath),
                    result.RecordCount);

                return;
            }

            _logger.LogWarning(
                "File '{FileName}' failed validation: {ValidationErrors}",
                Path.GetFileName(filePath),
                string.Join("; ", result.Errors));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process file '{FileName}'. The worker will continue with the next file.",
                Path.GetFileName(filePath));
        }
    }

    private bool TryMarkFileForProcessing(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath);

        if (_attemptedFileWriteTimes.TryGetValue(fullPath, out var attemptedWriteTimeUtc)
            && attemptedWriteTimeUtc == lastWriteTimeUtc)
        {
            return false;
        }

        _attemptedFileWriteTimes[fullPath] = lastWriteTimeUtc;
        return true;
    }

    private string ResolveIncomingFolder()
    {
        var inputFolder = string.IsNullOrWhiteSpace(_options.InputFolder)
            ? "Files/Incoming"
            : _options.InputFolder;

        return Path.GetFullPath(inputFolder);
    }

    private TimeSpan GetScanInterval()
    {
        var seconds = _options.ScanIntervalSeconds > 0
            ? _options.ScanIntervalSeconds
            : FileProcessingOptions.DefaultScanIntervalSeconds;

        return TimeSpan.FromSeconds(seconds);
    }

    private static bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
