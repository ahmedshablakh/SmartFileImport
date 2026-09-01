using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartFileImport.Api.Configuration;

namespace SmartFileImport.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private static readonly string[] SupportedExtensions =
    {
        ".csv",
        ".xlsx"
    };

    private readonly FileProcessingOptions _options;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IOptions<FileProcessingOptions> options,
        ILogger<FilesController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return BadRequest(new FileUploadErrorResponse("A file is required."));
        }

        if (file.Length == 0)
        {
            return BadRequest(new FileUploadErrorResponse("Uploaded file is empty."));
        }

        var fileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new FileUploadErrorResponse("Uploaded file must have a file name."));
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest(new FileUploadErrorResponse("Uploaded file name contains invalid characters."));
        }

        if (!IsSupportedFile(fileName))
        {
            return BadRequest(new FileUploadErrorResponse("Only .csv and .xlsx files are supported."));
        }

        var incomingFolder = ResolveIncomingFolder();

        try
        {
            Directory.CreateDirectory(incomingFolder);

            var destinationPath = BuildAvailableDestinationPath(incomingFolder, fileName);

            await using var stream = System.IO.File.Create(destinationPath);
            await file.CopyToAsync(stream, cancellationToken);

            var savedFileName = Path.GetFileName(destinationPath);

            _logger.LogInformation(
                "Uploaded file '{OriginalFileName}' was saved to incoming folder as '{SavedFileName}'.",
                fileName,
                savedFileName);

            return Accepted(new FileUploadResponse(
                savedFileName,
                "File uploaded successfully and queued for background processing."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Upload for file '{FileName}' was canceled.", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save uploaded file '{FileName}'.", fileName);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new FileUploadErrorResponse("Uploaded file could not be saved."));
        }
    }

    private string ResolveIncomingFolder()
    {
        var inputFolder = string.IsNullOrWhiteSpace(_options.InputFolder)
            ? "Files/Incoming"
            : _options.InputFolder;

        return Path.GetFullPath(inputFolder);
    }

    private static bool IsSupportedFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildAvailableDestinationPath(string destinationFolder, string fileName)
    {
        var destinationPath = Path.Combine(destinationFolder, fileName);

        if (!System.IO.File.Exists(destinationPath))
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

            if (!System.IO.File.Exists(destinationPath))
            {
                return destinationPath;
            }

            suffix++;
        }
    }
}

public sealed record FileUploadResponse(string FileName, string Message);

public sealed record FileUploadErrorResponse(string Error);
