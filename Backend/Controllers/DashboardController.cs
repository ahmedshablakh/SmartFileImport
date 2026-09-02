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
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext dbContext, ILogger<DashboardController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardStatisticsResponse>> Get(CancellationToken cancellationToken = default)
    {
        try
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard statistics.");

            return StatusCode(500, new DashboardErrorResponse("Dashboard statistics could not be loaded."));
        }
    }
}

public sealed record DashboardStatisticsResponse(
    int TotalFiles,
    int SuccessfulFiles,
    int FailedFiles,
    int TotalImportedRecords);

public sealed record DashboardErrorResponse(string Error);
