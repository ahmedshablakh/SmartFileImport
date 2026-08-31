using System.Globalization;
using ClosedXML.Excel;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class ExcelFileReader : IExcelFileReader
{
    private static readonly string[] RequiredColumns =
    {
        "Name",
        "Email",
        "Department",
        "Salary"
    };

    public IReadOnlyList<Employee> Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Excel file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Excel file was not found.", filePath);
        }

        if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"File '{Path.GetFileName(filePath)}' is not a supported Excel file.");
        }

        try
        {
            return ReadEmployees(filePath);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new IOException($"Excel file '{Path.GetFileName(filePath)}' could not be accessed.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Excel file '{Path.GetFileName(filePath)}' could not be read. Make sure it is a valid .xlsx file.",
                ex);
        }
    }

    private static IReadOnlyList<Employee> ReadEmployees(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException($"Excel file '{Path.GetFileName(filePath)}' does not contain a worksheet.");

        var headerRow = worksheet.FirstRowUsed()
            ?? throw new InvalidDataException($"Excel file '{Path.GetFileName(filePath)}' is empty.");

        var columnIndexes = BuildColumnIndexes(headerRow, filePath);
        var lastRow = worksheet.LastRowUsed();

        if (lastRow is null || lastRow.RowNumber() <= headerRow.RowNumber())
        {
            return Array.Empty<Employee>();
        }

        var employees = new List<Employee>();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow.RowNumber(); rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            if (IsRowEmpty(row, columnIndexes.Values))
            {
                continue;
            }

            employees.Add(new Employee
            {
                Name = GetValue(row, columnIndexes, "Name"),
                Email = GetValue(row, columnIndexes, "Email"),
                Department = GetValue(row, columnIndexes, "Department"),
                Salary = ParseSalary(row.Cell(columnIndexes["Salary"]), filePath, rowNumber),
                CreatedAt = DateTime.UtcNow
            });
        }

        return employees;
    }

    private static Dictionary<string, int> BuildColumnIndexes(IXLRow headerRow, string filePath)
    {
        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();

            if (!string.IsNullOrWhiteSpace(header) && !columnIndexes.ContainsKey(header))
            {
                columnIndexes.Add(header, cell.Address.ColumnNumber);
            }
        }

        var missingColumns = RequiredColumns
            .Where(requiredColumn => !columnIndexes.ContainsKey(requiredColumn))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidDataException(
                $"Excel file '{Path.GetFileName(filePath)}' is missing required column(s): {string.Join(", ", missingColumns)}.");
        }

        return columnIndexes;
    }

    private static string GetValue(
        IXLRow row,
        IReadOnlyDictionary<string, int> columnIndexes,
        string columnName)
    {
        return row.Cell(columnIndexes[columnName]).GetString().Trim();
    }

    private static bool IsRowEmpty(IXLRow row, IEnumerable<int> requiredColumnIndexes)
    {
        return requiredColumnIndexes
            .All(columnIndex => string.IsNullOrWhiteSpace(row.Cell(columnIndex).GetString()));
    }

    private static decimal ParseSalary(IXLCell cell, string filePath, int rowNumber)
    {
        if (cell.TryGetValue<decimal>(out var salary))
        {
            return salary;
        }

        var value = cell.GetString().Trim();

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out salary))
        {
            return salary;
        }

        throw new InvalidDataException(
            $"Excel file '{Path.GetFileName(filePath)}' has an invalid Salary value on row {rowNumber}: '{value}'.");
    }
}
