using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFileImport.Api.Controllers;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Tests;

public class DashboardControllerTests
{
    [Fact]
    public async Task Get_WhenImportHistoryExists_ReturnsCalculatedStatistics()
    {
        await using var dbContext = CreateDbContext();
        dbContext.ImportHistories.AddRange(
            new ImportHistory
            {
                FileName = "employees-1.csv",
                Status = "Success",
                RecordCount = 2,
                ProcessedAt = DateTime.UtcNow
            },
            new ImportHistory
            {
                FileName = "employees-2.xlsx",
                Status = "Success",
                RecordCount = 3,
                ProcessedAt = DateTime.UtcNow
            },
            new ImportHistory
            {
                FileName = "invalid.csv",
                Status = "Failed",
                RecordCount = 99,
                ProcessedAt = DateTime.UtcNow,
                ErrorMessage = "Record 1: Name is required."
            });
        await dbContext.SaveChangesAsync();
        var controller = new DashboardController(dbContext, NullLogger<DashboardController>.Instance);

        var result = await controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DashboardStatisticsResponse>(okResult.Value);
        Assert.Equal(3, response.TotalFiles);
        Assert.Equal(2, response.SuccessfulFiles);
        Assert.Equal(1, response.FailedFiles);
        Assert.Equal(5, response.TotalImportedRecords);
    }

    [Fact]
    public async Task Get_WhenNoImportHistoryExists_ReturnsZeroStatistics()
    {
        await using var dbContext = CreateDbContext();
        var controller = new DashboardController(dbContext, NullLogger<DashboardController>.Instance);

        var result = await controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DashboardStatisticsResponse>(okResult.Value);
        Assert.Equal(0, response.TotalFiles);
        Assert.Equal(0, response.SuccessfulFiles);
        Assert.Equal(0, response.FailedFiles);
        Assert.Equal(0, response.TotalImportedRecords);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
