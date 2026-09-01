using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Services;

namespace SmartFileImport.Api.Tests;

public sealed class FileImportServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public FileImportServiceTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task ImportAsync_WhenCsvIsValid_SavesEmployees()
    {
        var filePath = WriteCsv(
            "employees.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            Sara Example,sara@example.com,Finance,5200
            """);
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.ImportAsync(filePath);

        var savedEmployees = await dbContext.Employees
            .OrderBy(employee => employee.Name)
            .ToListAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RecordCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, savedEmployees.Count);
        Assert.Equal("Ahmed Shablakh", savedEmployees[0].Name);
        Assert.Equal("Sara Example", savedEmployees[1].Name);
    }

    [Fact]
    public async Task ImportAsync_WhenExcelIsValid_SavesEmployees()
    {
        var filePath = WriteWorkbook(
            "employees.xlsx",
            worksheet =>
            {
                AddHeaders(worksheet);
                worksheet.Cell(2, 1).Value = "Ahmed Shablakh";
                worksheet.Cell(2, 2).Value = "ahmed@example.com";
                worksheet.Cell(2, 3).Value = "Engineering";
                worksheet.Cell(2, 4).Value = 4500.50m;
            });
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.ImportAsync(filePath);

        var savedEmployee = await dbContext.Employees.SingleAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RecordCount);
        Assert.Empty(result.Errors);
        Assert.Equal("Ahmed Shablakh", savedEmployee.Name);
        Assert.Equal(4500.50m, savedEmployee.Salary);
    }

    [Fact]
    public async Task ImportAsync_WhenValidationFails_DoesNotSaveAnyEmployees()
    {
        var filePath = WriteCsv(
            "invalid-employees.csv",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            Sara Example,not-an-email,Finance,5200
            """);
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.ImportAsync(filePath);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.RecordCount);
        Assert.Contains("Record 2: Email must have a valid format.", result.Errors);
        Assert.Equal(0, await dbContext.Employees.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_WhenFileTypeIsUnsupported_ThrowsClearError()
    {
        var filePath = WriteCsv(
            "employees.txt",
            """
            Name,Email,Department,Salary
            Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
            """);
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ImportAsync(filePath));

        Assert.Contains("unsupported file type", exception.Message);
        Assert.Contains(".csv and .xlsx", exception.Message);
        Assert.Equal(0, await dbContext.Employees.CountAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FileImportService CreateService(ApplicationDbContext dbContext)
    {
        return new FileImportService(
            dbContext,
            new CsvFileReader(),
            new ExcelFileReader(),
            new EmployeeValidator());
    }

    private string WriteCsv(string fileName, string contents)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, contents);
        return filePath;
    }

    private string WriteWorkbook(string fileName, Action<IXLWorksheet> buildWorksheet)
    {
        var filePath = Path.Combine(_testDirectory, fileName);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Employees");
        buildWorksheet(worksheet);
        workbook.SaveAs(filePath);

        return filePath;
    }

    private static void AddHeaders(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "Email";
        worksheet.Cell(1, 3).Value = "Department";
        worksheet.Cell(1, 4).Value = "Salary";
    }
}
