namespace SmartFileImport.Api.Services;

public interface IFileImportService
{
    Task<FileImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
