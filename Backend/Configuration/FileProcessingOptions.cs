namespace SmartFileImport.Api.Configuration;

public class FileProcessingOptions
{
    public const string SectionName = "FileProcessing";

    public const int DefaultScanIntervalSeconds = 5;

    public string InputFolder { get; set; } = "Files/Incoming";

    public string ProcessedFolder { get; set; } = "Files/Processed";

    public string ErrorFolder { get; set; } = "Files/Error";

    public int ScanIntervalSeconds { get; set; } = DefaultScanIntervalSeconds;
}
