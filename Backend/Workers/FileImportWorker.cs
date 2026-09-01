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

        _logger.LogDebug("Scanning incoming folder '{IncomingFolder}'.", incomingFolder);

        var files = Directory.EnumerateFiles(incomingFolder)
            .Where(IsSupportedFile)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogDebug("No supported files found in incoming folder '{IncomingFolder}'.", incomingFolder);
            return;
        }

        _logger.LogInformation(
            "Found {FileCount} supported file(s) in incoming folder '{IncomingFolder}'.",
            files.Length,
            incomingFolder);

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryMarkFileForProcessing(filePath))
            {
                _logger.LogDebug(
                    "Skipping file '{FileName}' because it has already been attempted and has not changed.",
                    Path.GetFileName(filePath));

                continue;
            }

            _logger.LogInformation("Detected file '{FileName}' for import.", Path.GetFileName(filePath));
            await ProcessFileAsync(filePath, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "File import worker started. Incoming folder: '{IncomingFolder}'. Scan interval: {ScanIntervalSeconds} second(s).",
            ResolveIncomingFolder(),
            GetScanInterval().TotalSeconds);

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

        _logger.LogInformation("File import worker stopped.");
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

                MoveFile(filePath, ResolveProcessedFolder(), "processed");
                return;
            }

            _logger.LogWarning(
                "File '{FileName}' failed validation: {ValidationErrors}",
                Path.GetFileName(filePath),
                string.Join("; ", result.Errors));

            MoveFile(filePath, ResolveErrorFolder(), "error");
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

            MoveFile(filePath, ResolveErrorFolder(), "error");
        }
    }

    private void MoveFile(string filePath, string destinationFolder, string destinationName)
    {
        try
        {
            Directory.CreateDirectory(destinationFolder);

            var destinationPath = BuildAvailableDestinationPath(destinationFolder, Path.GetFileName(filePath));
            File.Move(filePath, destinationPath);

            _logger.LogInformation(
                "Moved file '{FileName}' to {DestinationName} folder at '{DestinationPath}'.",
                Path.GetFileName(filePath),
                destinationName,
                destinationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move file '{FileName}' to {DestinationName} folder. The worker will continue.",
                Path.GetFileName(filePath),
                destinationName);
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
        return ResolveFolder(_options.InputFolder, "Files/Incoming");
    }

    private string ResolveProcessedFolder()
    {
        return ResolveFolder(_options.ProcessedFolder, "Files/Processed");
    }

    private string ResolveErrorFolder()
    {
        return ResolveFolder(_options.ErrorFolder, "Files/Error");
    }

    private static string ResolveFolder(string configuredFolder, string fallbackFolder)
    {
        var folder = string.IsNullOrWhiteSpace(configuredFolder)
            ? fallbackFolder
            : configuredFolder;

        return Path.GetFullPath(folder);
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

    private static string BuildAvailableDestinationPath(string destinationFolder, string fileName)
    {
        var destinationPath = Path.Combine(destinationFolder, fileName);

        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 1;

        while (true)
        {
            destinationPath = Path.Combine(
                destinationFolder,
                $"{fileNameWithoutExtension}_{suffix}{extension}");

            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }

            suffix++;
        }
    }
}
