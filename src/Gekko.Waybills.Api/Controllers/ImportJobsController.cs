using Gekko.Waybills.Domain;
using Gekko.Waybills.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Api.Controllers;

[ApiController]
[Route("api/import-jobs")]
public sealed class ImportJobsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ImportJobsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns the status of an import job.</summary>
    /// <param name="id">Import job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the import job status.</response>
    /// <response code="404">Import job not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImportJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportJobDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _dbContext.ImportJobs
            .Where(j => j.Id == id)
            .Select(j => new ImportJobDto
            {
                Id = j.Id,
                Status = j.Status,
                TotalRows = j.TotalRows,
                InsertedCount = j.InsertedCount,
                UpdatedCount = j.UpdatedCount,
                RejectedCount = j.RejectedCount,
                ProgressPercent = j.ProgressPercent,
                Error = j.Error,
                CreatedAtUtc = j.CreatedAtUtc,
                UpdatedAtUtc = j.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    /// <summary>Import job response.</summary>
    public sealed class ImportJobDto
    {
        public Guid Id { get; set; }
        public ImportJobStatus Status { get; set; }
        public int? TotalRows { get; set; }
        public int? InsertedCount { get; set; }
        public int? UpdatedCount { get; set; }
        public int? RejectedCount { get; set; }
        public int? ProgressPercent { get; set; }
        public string? Error { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
