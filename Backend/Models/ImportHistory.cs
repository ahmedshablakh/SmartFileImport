namespace SmartFileImport.Api.Models;

public class ImportHistory
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int RecordCount { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }
}
