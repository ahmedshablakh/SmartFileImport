using SmartFileImport.Api.Services;

namespace SmartFileImport.Api.Tests;

public sealed class CsvFileReaderTests : IDisposable
{
    private readonly CsvFileReader _reader = new();
    private readonly string _testDirectory;

    public CsvFileReaderTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Read_WhenCsvIsValid_ReturnsEmployees()
    {
        var filePath = WriteCsv(
            "employees.csv",
            """
            Name,Email,Department,Salary
            "Ahmed Shablakh",ahmed@example.com,Engineering,4500.50
            "Sara Example",sara@example.com,Finance,5200
            """);

        var employees = _reader.Read(filePath);

        Assert.Equal(2, employees.Count);
        Assert.Equal("Ahmed Shablakh", employees[0].Name);
        Assert.Equal("ahmed@example.com", employees[0].Email);
        Assert.Equal("Engineering", employees[0].Department);
        Assert.Equal(4500.50m, employees[0].Salary);
        Assert.Equal("Sara Example", employees[1].Name);
        Assert.Equal(5200m, employees[1].Salary);
    }

    [Fact]
    public void Read_WhenCsvHasQuotedComma_ParsesFieldCorrectly()
    {
        var filePath = WriteCsv(
            "quoted.csv",
            """
            Name,Email,Department,Salary
            "Ahmed, Jr.",ahmed.jr@example.com,Engineering,4500.50
            """);

        var employees = _reader.Read(filePath);

        Assert.Single(employees);
        Assert.Equal("Ahmed, Jr.", employees[0].Name);
    }

    [Fact]
    public void Read_WhenRequiredHeaderIsMissing_ThrowsClearError()
    {
        var filePath = WriteCsv(
            "missing-header.csv",
            """
            Name,Email,Salary
            Ahmed Shablakh,ahmed@example.com,4500.50
            """);

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("missing required column", exception.Message);
        Assert.Contains("Department", exception.Message);
    }

    [Fact]
    public void Read_WhenSalaryIsInvalid_ThrowsClearError()
    {
        var filePath = WriteCsv(
            "invalid-salary.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,not-a-number
            """);

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("invalid Salary value", exception.Message);
        Assert.Contains("row 2", exception.Message);
    }

    [Fact]
    public void Read_WhenFileExtensionIsNotCsv_ThrowsClearError()
    {
        var filePath = WriteCsv(
            "employees.txt",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            """);

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("not a supported CSV file", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string WriteCsv(string fileName, string contents)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, contents);
        return filePath;
    }
}
