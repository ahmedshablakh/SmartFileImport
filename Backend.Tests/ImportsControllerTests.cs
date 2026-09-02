using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFileImport.Api.Controllers;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Tests;

public class ImportsControllerTests
{
    [Fact]
    public async Task Get_WhenImportHistoryExists_ReturnsRecordsInNewestFirstOrder()
    {
        await using var dbContext = CreateDbContext();
        var olderImport = new ImportHistory
        {
            FileName = "employees-old.csv",
            Status = "Success",
            RecordCount = 2,
            ProcessedAt = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc)
        };
        var newerImport = new ImportHistory
        {
            FileName = "employees-new.xlsx",
            Status = "Failed",
            RecordCount = 0,
            ProcessedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            ErrorMessage = "Record 1: Email is invalid."
        };
        dbContext.ImportHistories.AddRange(olderImport, newerImport);
        await dbContext.SaveChangesAsync();
        var controller = new ImportsController(dbContext, NullLogger<ImportsController>.Instance);

        var result = await controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<ImportHistoryResponse>>(okResult.Value);
        Assert.Collection(
            response,
            import =>
            {
                Assert.Equal(newerImport.Id, import.Id);
                Assert.Equal("employees-new.xlsx", import.FileName);
                Assert.Equal("Failed", import.Status);
                Assert.Equal(0, import.RecordCount);
                Assert.Equal(newerImport.ProcessedAt, import.ProcessedAt);
                Assert.Equal("Record 1: Email is invalid.", import.ErrorMessage);
            },
            import =>
            {
                Assert.Equal(olderImport.Id, import.Id);
                Assert.Equal("employees-old.csv", import.FileName);
                Assert.Equal("Success", import.Status);
                Assert.Equal(2, import.RecordCount);
                Assert.Equal(olderImport.ProcessedAt, import.ProcessedAt);
                Assert.Null(import.ErrorMessage);
            });
    }

    [Fact]
    public async Task GetById_WhenImportHistoryExists_ReturnsRecord()
    {
        await using var dbContext = CreateDbContext();
        var failedImport = new ImportHistory
        {
            FileName = "invalid.csv",
            Status = "Failed",
            RecordCount = 0,
            ProcessedAt = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
            ErrorMessage = "Record 2: Salary must be greater than zero."
        };
        dbContext.ImportHistories.Add(failedImport);
        await dbContext.SaveChangesAsync();
        var controller = new ImportsController(dbContext, NullLogger<ImportsController>.Instance);

        var result = await controller.GetById(failedImport.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ImportHistoryResponse>(okResult.Value);
        Assert.Equal(failedImport.Id, response.Id);
        Assert.Equal("invalid.csv", response.FileName);
        Assert.Equal("Failed", response.Status);
        Assert.Equal(0, response.RecordCount);
        Assert.Equal(failedImport.ProcessedAt, response.ProcessedAt);
        Assert.Equal("Record 2: Salary must be greater than zero.", response.ErrorMessage);
    }

    [Fact]
    public async Task GetById_WhenImportHistoryDoesNotExist_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var controller = new ImportsController(dbContext, NullLogger<ImportsController>.Instance);

        var result = await controller.GetById(123);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ImportHistoryErrorResponse>(notFoundResult.Value);
        Assert.Contains("not found", response.Error);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
