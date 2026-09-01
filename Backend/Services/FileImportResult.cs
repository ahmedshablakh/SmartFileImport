namespace SmartFileImport.Api.Services;

public sealed record FileImportResult(bool Succeeded, int RecordCount, IReadOnlyList<string> Errors)
{
    public static FileImportResult Success(int recordCount)
    {
        return new FileImportResult(true, recordCount, Array.Empty<string>());
    }

    public static FileImportResult Failed(IReadOnlyList<string> errors)
    {
        return new FileImportResult(false, 0, errors.ToArray());
    }
}
