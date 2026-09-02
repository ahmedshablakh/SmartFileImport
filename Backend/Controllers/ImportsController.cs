using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Data;

namespace SmartFileImport.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ImportsController> _logger;

    public ImportsController(ApplicationDbContext dbContext, ILogger<ImportsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportHistoryResponse>>> Get(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var imports = await _dbContext.ImportHistories
                .AsNoTracking()
                .OrderByDescending(importHistory => importHistory.ProcessedAt)
                .ThenByDescending(importHistory => importHistory.Id)
                .Select(importHistory => new ImportHistoryResponse(
                    importHistory.Id,
                    importHistory.FileName,
                    importHistory.Status,
                    importHistory.RecordCount,
                    importHistory.ProcessedAt,
                    importHistory.ErrorMessage))
                .ToListAsync(cancellationToken);

            return Ok(imports);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load import history.");

            return StatusCode(500, new ImportHistoryErrorResponse("Import history could not be loaded."));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImportHistoryResponse>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var import = await _dbContext.ImportHistories
                .AsNoTracking()
                .Where(importHistory => importHistory.Id == id)
                .Select(importHistory => new ImportHistoryResponse(
                    importHistory.Id,
                    importHistory.FileName,
                    importHistory.Status,
                    importHistory.RecordCount,
                    importHistory.ProcessedAt,
                    importHistory.ErrorMessage))
                .FirstOrDefaultAsync(cancellationToken);

            if (import is null)
            {
                return NotFound(new ImportHistoryErrorResponse("Import history record was not found."));
            }

            return Ok(import);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load import history record {ImportHistoryId}.", id);

            return StatusCode(500, new ImportHistoryErrorResponse("Import history record could not be loaded."));
        }
    }
}

public sealed record ImportHistoryResponse(
    int Id,
    string FileName,
    string Status,
    int RecordCount,
    DateTime ProcessedAt,
    string? ErrorMessage);

public sealed record ImportHistoryErrorResponse(string Error);
