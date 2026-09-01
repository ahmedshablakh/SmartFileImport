using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Data;

namespace SmartFileImport.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private const string SuccessStatus = "Success";
    private const string FailedStatus = "Failed";

    private readonly ApplicationDbContext _dbContext;

    public DashboardController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardStatisticsResponse>> Get(CancellationToken cancellationToken = default)
    {
        var totalFiles = await _dbContext.ImportHistories.CountAsync(cancellationToken);
        var successfulFiles = await _dbContext.ImportHistories
            .CountAsync(importHistory => importHistory.Status == SuccessStatus, cancellationToken);
        var failedFiles = await _dbContext.ImportHistories
            .CountAsync(importHistory => importHistory.Status == FailedStatus, cancellationToken);
        var totalImportedRecords = await _dbContext.ImportHistories
            .Where(importHistory => importHistory.Status == SuccessStatus)
            .SumAsync(importHistory => (int?)importHistory.RecordCount, cancellationToken)
            ?? 0;

        return Ok(new DashboardStatisticsResponse(
            totalFiles,
            successfulFiles,
            failedFiles,
            totalImportedRecords));
    }
}

public sealed record DashboardStatisticsResponse(
    int TotalFiles,
    int SuccessfulFiles,
    int FailedFiles,
    int TotalImportedRecords);
