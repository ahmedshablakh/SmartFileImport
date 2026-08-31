using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Services;

public class CsvFileReader : ICsvFileReader
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
            throw new ArgumentException("CSV file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file was not found.", filePath);
        }

        if (!string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"File '{Path.GetFileName(filePath)}' is not a supported CSV file.");
        }

        try
        {
            return ReadEmployees(filePath);
        }
        catch (MalformedLineException ex)
        {
            throw new InvalidDataException(
                $"CSV file '{Path.GetFileName(filePath)}' contains malformed data near line {ex.LineNumber}.",
                ex);
        }
    }

    private static IReadOnlyList<Employee> ReadEmployees(string filePath)
    {
        using var parser = new TextFieldParser(filePath, Encoding.UTF8, detectEncoding: true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };

        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new InvalidDataException($"CSV file '{Path.GetFileName(filePath)}' is empty.");
        }

        var headers = parser.ReadFields();
        var columnIndexes = BuildColumnIndexes(headers, filePath);
        var employees = new List<Employee>();
        var rowNumber = 1;

        while (!parser.EndOfData)
        {
            rowNumber++;

            var fields = parser.ReadFields();

            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            employees.Add(new Employee
            {
                Name = GetValue(fields, columnIndexes, "Name"),
                Email = GetValue(fields, columnIndexes, "Email"),
                Department = GetValue(fields, columnIndexes, "Department"),
                Salary = ParseSalary(GetValue(fields, columnIndexes, "Salary"), filePath, rowNumber),
                CreatedAt = DateTime.UtcNow
            });
        }

        return employees;
    }

    private static Dictionary<string, int> BuildColumnIndexes(string[]? headers, string filePath)
    {
        if (headers is null || headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"CSV file '{Path.GetFileName(filePath)}' does not contain a header row.");
        }

        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index].Trim();

            if (!string.IsNullOrWhiteSpace(header) && !columnIndexes.ContainsKey(header))
            {
                columnIndexes.Add(header, index);
            }
        }

        var missingColumns = RequiredColumns
            .Where(requiredColumn => !columnIndexes.ContainsKey(requiredColumn))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidDataException(
                $"CSV file '{Path.GetFileName(filePath)}' is missing required column(s): {string.Join(", ", missingColumns)}.");
        }

        return columnIndexes;
    }

    private static string GetValue(
        IReadOnlyList<string?> fields,
        IReadOnlyDictionary<string, int> columnIndexes,
        string columnName)
    {
        var columnIndex = columnIndexes[columnName];

        if (columnIndex >= fields.Count)
        {
            return string.Empty;
        }

        return fields[columnIndex]?.Trim() ?? string.Empty;
    }

    private static decimal ParseSalary(string value, string filePath, int rowNumber)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var salary))
        {
            return salary;
        }

        throw new InvalidDataException(
            $"CSV file '{Path.GetFileName(filePath)}' has an invalid Salary value on row {rowNumber}: '{value}'.");
    }
}
