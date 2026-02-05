using Gekko.Waybills.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Api.Controllers;

[ApiController]
[Route("api/import-audits")]
public sealed class ImportAuditsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ImportAuditsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns the latest import audit rows for the current tenant.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the last 20 import audits.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ImportAuditDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ImportAuditDto>>> GetLatest(CancellationToken cancellationToken)
    {
        var items = await _dbContext.ImportAudits
            .OrderByDescending(a => a.ReceivedAtUtc)
            .Take(20)
            .Select(a => new ImportAuditDto
            {
                ImportJobId = a.ImportJobId,
                TotalRows = a.TotalRows,
                InsertedCount = a.InsertedCount,
                UpdatedCount = a.UpdatedCount,
                RejectedCount = a.RejectedCount,
                ReceivedAtUtc = a.ReceivedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Import audit response.</summary>
    public sealed class ImportAuditDto
    {
        /// <summary>Import job identifier.</summary>
        public Guid ImportJobId { get; set; }

        /// <summary>Total rows in the import.</summary>
        public int TotalRows { get; set; }

        /// <summary>Inserted row count.</summary>
        public int InsertedCount { get; set; }

        /// <summary>Updated row count.</summary>
        public int UpdatedCount { get; set; }

        /// <summary>Rejected row count.</summary>
        public int RejectedCount { get; set; }

        /// <summary>UTC time when the audit was received.</summary>
        public DateTime ReceivedAtUtc { get; set; }
    }
}
