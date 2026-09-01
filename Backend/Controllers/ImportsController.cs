using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Data;

namespace SmartFileImport.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public ImportsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportHistoryResponse>>> Get(
        CancellationToken cancellationToken = default)
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImportHistoryResponse>> GetById(
        int id,
        CancellationToken cancellationToken = default)
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
}

public sealed record ImportHistoryResponse(
    int Id,
    string FileName,
    string Status,
    int RecordCount,
    DateTime ProcessedAt,
    string? ErrorMessage);

public sealed record ImportHistoryErrorResponse(string Error);
