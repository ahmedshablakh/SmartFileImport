using ClosedXML.Excel;
using SmartFileImport.Api.Services;

namespace SmartFileImport.Api.Tests;

public sealed class ExcelFileReaderTests : IDisposable
{
    private readonly ExcelFileReader _reader = new();
    private readonly string _testDirectory;

    public ExcelFileReaderTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SmartFileImportTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Read_WhenWorkbookIsValid_ReturnsEmployees()
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
                worksheet.Cell(3, 1).Value = "Sara Example";
                worksheet.Cell(3, 2).Value = "sara@example.com";
                worksheet.Cell(3, 3).Value = "Finance";
                worksheet.Cell(3, 4).Value = 5200m;
            });

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
    public void Read_WhenWorkbookContainsBlankRows_SkipsBlankRows()
    {
        var filePath = WriteWorkbook(
            "blank-rows.xlsx",
            worksheet =>
            {
                AddHeaders(worksheet);
                worksheet.Cell(2, 1).Value = "Ahmed Shablakh";
                worksheet.Cell(2, 2).Value = "ahmed@example.com";
                worksheet.Cell(2, 3).Value = "Engineering";
                worksheet.Cell(2, 4).Value = 4500.50m;
                worksheet.Cell(3, 1).Value = string.Empty;
                worksheet.Cell(3, 2).Value = string.Empty;
                worksheet.Cell(3, 3).Value = string.Empty;
                worksheet.Cell(3, 4).Value = string.Empty;
            });

        var employees = _reader.Read(filePath);

        Assert.Single(employees);
    }

    [Fact]
    public void Read_WhenRequiredHeaderIsMissing_ThrowsClearError()
    {
        var filePath = WriteWorkbook(
            "missing-header.xlsx",
            worksheet =>
            {
                worksheet.Cell(1, 1).Value = "Name";
                worksheet.Cell(1, 2).Value = "Email";
                worksheet.Cell(1, 3).Value = "Salary";
                worksheet.Cell(2, 1).Value = "Ahmed Shablakh";
                worksheet.Cell(2, 2).Value = "ahmed@example.com";
                worksheet.Cell(2, 3).Value = 4500.50m;
            });

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("missing required column", exception.Message);
        Assert.Contains("Department", exception.Message);
    }

    [Fact]
    public void Read_WhenSalaryIsInvalid_ThrowsClearError()
    {
        var filePath = WriteWorkbook(
            "invalid-salary.xlsx",
            worksheet =>
            {
                AddHeaders(worksheet);
                worksheet.Cell(2, 1).Value = "Ahmed Shablakh";
                worksheet.Cell(2, 2).Value = "ahmed@example.com";
                worksheet.Cell(2, 3).Value = "Engineering";
                worksheet.Cell(2, 4).Value = "not-a-number";
            });

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("invalid Salary value", exception.Message);
        Assert.Contains("row 2", exception.Message);
    }

    [Fact]
    public void Read_WhenFileExtensionIsNotXlsx_ThrowsClearError()
    {
        var filePath = Path.Combine(_testDirectory, "employees.xls");
        File.WriteAllText(filePath, "not an xlsx file");

        var exception = Assert.Throws<InvalidDataException>(() => _reader.Read(filePath));

        Assert.Contains("not a supported Excel file", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
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
