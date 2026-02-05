using Gekko.Waybills.Application.Imports;
using Gekko.Waybills.Application.Locks;
using Gekko.Waybills.Application.Queries;
using Gekko.Waybills.Api.Services;
using Gekko.Waybills.Domain;
using Gekko.Waybills.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gekko.Waybills.Api.Controllers;

[ApiController]
[Route("api/waybills")]
public sealed class WaybillsController : ControllerBase
{
    private readonly IWaybillImportService _importService;
    private readonly IWaybillQueryService _queryService;
    private readonly IExecutionLockService _lockService;
    private readonly AppDbContext _dbContext;
    private readonly IImportJobQueue _importJobQueue;
    private readonly IMemoryCache _cache;
    private readonly ISummaryCacheVersionProvider _summaryCacheVersionProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WaybillsController> _logger;
    private readonly CacheOptions _cacheOptions;

    public WaybillsController(
        IWaybillImportService importService,
        IWaybillQueryService queryService,
        IExecutionLockService lockService,
        AppDbContext dbContext,
        IImportJobQueue importJobQueue,
        IMemoryCache cache,
        ISummaryCacheVersionProvider summaryCacheVersionProvider,
        ITenantContext tenantContext,
        ILogger<WaybillsController> logger,
        IOptions<CacheOptions> cacheOptions)
    {
        _importService = importService;
        _queryService = queryService;
        _lockService = lockService;
        _dbContext = dbContext;
        _importJobQueue = importJobQueue;
        _cache = cache;
        _summaryCacheVersionProvider = summaryCacheVersionProvider;
        _tenantContext = tenantContext;
        _logger = logger;
        _cacheOptions = cacheOptions.Value;
    }

    /// <summary>Returns paged waybills with optional filters.</summary>
    /// <param name="parameters">Query filters and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the paged list of waybills.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<WaybillListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<WaybillListItemDto>>> GetWaybills(
        [FromQuery] WaybillQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var version = _summaryCacheVersionProvider.GetVersion(tenantId);
        var queryKey = HttpContext.Request.QueryString.Value ?? string.Empty;
        var cacheKey = $"waybills:{tenantId}:{version}:{queryKey}";
        if (_cache.TryGetValue(cacheKey, out PagedResultDto<WaybillListItemDto> cached))
        {
            _logger.LogInformation("Waybills cache HIT Tenant={TenantId} Key={CacheKey}", tenantId, cacheKey);
            return Ok(cached);
        }

        _logger.LogInformation("Waybills cache MISS Tenant={TenantId} Key={CacheKey}", tenantId, cacheKey);
        var result = await _queryService.GetWaybillsAsync(parameters, cancellationToken);
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_cacheOptions.DefaultTtlSeconds));
        return Ok(result);
    }

    /// <summary>Returns a single waybill.</summary>
    /// <param name="id">Waybill identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the waybill.</response>
    /// <response code="404">Waybill not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WaybillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaybillDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryService.GetWaybillByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>Returns aggregated waybill summaries.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns summary data.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(WaybillSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WaybillSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var version = _summaryCacheVersionProvider.GetVersion(tenantId);
        var queryKey = HttpContext.Request.QueryString.Value ?? string.Empty;
        var cacheKey = $"summary:{tenantId}:{version}:{queryKey}";

        if (_cache.TryGetValue(cacheKey, out WaybillSummaryDto cached))
        {
            _logger.LogInformation("Summary cache HIT Tenant={TenantId} Key={CacheKey}", tenantId, cacheKey);
            return Ok(cached);
        }

        _logger.LogInformation("Summary cache MISS Tenant={TenantId} Key={CacheKey}", tenantId, cacheKey);
        var result = await _queryService.GetWaybillSummaryAsync(cancellationToken);
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_cacheOptions.DefaultTtlSeconds));
        return Ok(result);
    }

    /// <summary>Updates a waybill with optimistic concurrency.</summary>
    /// <param name="id">Waybill identifier.</param>
    /// <param name="request">Update payload including row version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the updated waybill.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Waybill not found.</response>
    /// <response code="409">Waybill was modified by another user.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WaybillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] WaybillUpdateRequest body,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waybill update attempt Id={WaybillId} Tenant={TenantId}", id, _tenantContext.TenantId);
        if (body is null || string.IsNullOrWhiteSpace(body.RowVersionBase64))
        {
            _logger.LogWarning("Waybill update validation failed Id={WaybillId} Tenant={TenantId} Reason=RowVersionMissing",
                id, _tenantContext.TenantId);
            return BadRequest(new { error = "rowVersionBase64 is required." });
        }

        byte[] originalRowVersion;
        try
        {
            originalRowVersion = Convert.FromBase64String(body.RowVersionBase64);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Waybill update validation failed Id={WaybillId} Tenant={TenantId} Reason=RowVersionInvalid",
                id, _tenantContext.TenantId);
            return BadRequest(new { error = "rowVersionBase64 is invalid." });
        }

        var waybill = await _dbContext.Waybills
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (waybill is null)
        {
            return NotFound();
        }

        if (body.Quantity < 0.5m || body.Quantity > 50m)
        {
            _logger.LogWarning("Waybill update validation failed Id={WaybillId} Tenant={TenantId} Reason=QuantityOutOfRange",
                id, _tenantContext.TenantId);
            return BadRequest(new { error = "Quantity must be between 0.5 and 50." });
        }

        if (body.DeliveryDate < waybill.WaybillDate)
        {
            _logger.LogWarning("Waybill update validation failed Id={WaybillId} Tenant={TenantId} Reason=DeliveryDateBeforeWaybill",
                id, _tenantContext.TenantId);
            return BadRequest(new { error = "DeliveryDate must be on or after WaybillDate." });
        }

        var expectedTotal = body.Quantity * body.UnitPrice;
        if (Math.Abs(expectedTotal - body.TotalAmount) > 0.01m)
        {
            _logger.LogWarning("Waybill update validation failed Id={WaybillId} Tenant={TenantId} Reason=TotalMismatch",
                id, _tenantContext.TenantId);
            return BadRequest(new { error = "TotalAmount must equal Quantity * UnitPrice." });
        }

        if (!WaybillStatusTransitions.IsValidTransition(waybill.Status, body.Status))
        {
            _logger.LogWarning(
                "Invalid status transition Id={WaybillId} Tenant={TenantId} From={FromStatus} To={ToStatus}",
                id,
                _tenantContext.TenantId,
                waybill.Status,
                body.Status);
            return BadRequest(new { error = "INVALID_STATUS_TRANSITION" });
        }

        waybill.DeliveryDate = body.DeliveryDate;
        waybill.ProductCode = body.ProductCode.Trim();
        waybill.Quantity = body.Quantity;
        waybill.UnitPrice = body.UnitPrice;
        waybill.TotalAmount = body.TotalAmount;
        waybill.Status = body.Status;

        var entry = _dbContext.Entry(waybill);
        entry.Property(w => w.RowVersion).OriginalValue = originalRowVersion;
        // Ensure UPDATE executes so concurrency check runs even if values are unchanged.
        entry.State = EntityState.Modified;
        entry.Property(w => w.RowVersion).IsModified = false;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Waybill update concurrency conflict Id={WaybillId} Tenant={TenantId}", id, _tenantContext.TenantId);
            return Conflict(new { error = "Waybill was modified by another user. Please reload." });
        }

        _summaryCacheVersionProvider.Increment(_tenantContext.TenantId, "waybill-update");
        _logger.LogInformation("Waybill updated Id={WaybillId} Tenant={TenantId}", id, _tenantContext.TenantId);
        var updated = await _queryService.GetWaybillByIdAsync(id, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    /// <summary>Generates the monthly report for the current tenant.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns report generation metadata.</response>
    /// <response code="409">Report generation is already running.</response>
    [HttpPost("generate-monthly-report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> GenerateMonthlyReport(CancellationToken cancellationToken)
    {
        const string lockName = "MONTHLY_REPORT";
        var acquired = await _lockService.TryAcquireAsync(lockName, TimeSpan.FromMinutes(10), cancellationToken);
        if (!acquired)
        {
            return Conflict(new { error = "Report generation is already running" });
        }

        var startedAt = DateTime.UtcNow;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
        finally
        {
            await _lockService.ReleaseAsync(lockName, cancellationToken);
        }

        return Ok(new
        {
            tenantId = HttpContext.Request.Headers["X-Tenant-ID"].ToString(),
            generatedAtUtc = DateTime.UtcNow,
            durationSeconds = 15
        });
    }

    /// <summary>Imports waybills from a CSV file.</summary>
    /// <param name="file">CSV file uploaded under the form field name "file".</param>
    /// <param name="asyncImport">When true, queues the import as a background job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns import summary with warnings and rejected rows.</response>
    /// <response code="202">Returns the queued import job identifier.</response>
    /// <response code="400">File is missing or invalid.</response>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(WaybillImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WaybillImportResultDto>> Import(
        [FromForm] WaybillsImportRequest request,
        [FromQuery(Name = "async")] bool asyncImport,
        CancellationToken cancellationToken)
    {
        if (request?.File is null || request.File.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        if (asyncImport)
        {
            var job = new ImportJob
            {
                Id = Guid.NewGuid(),
                TenantId = HttpContext.Request.Headers["X-Tenant-ID"].ToString(),
                Status = ImportJobStatus.QUEUED,
                ProgressPercent = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.ImportJobs.Add(job);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Import job created JobId={JobId} Tenant={TenantId}", job.Id, job.TenantId);

            await using var stream = request.File.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            await _importJobQueue.QueueAsync(new ImportJobWorkItem
            {
                JobId = job.Id,
                TenantId = job.TenantId,
                CsvData = memory.ToArray()
            }, cancellationToken);

            return Accepted(new { jobId = job.Id });
        }

        _logger.LogInformation("CSV import started Tenant={TenantId} Mode=Sync", _tenantContext.TenantId);
        await using var syncStream = request.File.OpenReadStream();
        var result = await _importService.ImportAsync(syncStream, cancellationToken);
        _summaryCacheVersionProvider.Increment(_tenantContext.TenantId, "import-sync");
        _logger.LogInformation(
            "CSV import completed Tenant={TenantId} Total={Total} Inserted={Inserted} Updated={Updated} Rejected={Rejected}",
            _tenantContext.TenantId,
            result.TotalRows,
            result.InsertedCount,
            result.UpdatedCount,
            result.RejectedCount);
        return Ok(result);
    }

    /// <summary>Request payload for waybill CSV import.</summary>
    public sealed class WaybillsImportRequest
    {
        /// <summary>CSV file uploaded under the form field name "file".</summary>
        [FromForm(Name = "file")]
        public IFormFile File { get; set; } = null!;
    }

    /// <summary>Waybill update payload.</summary>
    public sealed class WaybillUpdateRequest
    {
        /// <summary>Row version encoded as base64.</summary>
        public string RowVersionBase64 { get; set; } = string.Empty;

        /// <summary>Delivery date.</summary>
        public DateOnly DeliveryDate { get; set; }

        /// <summary>Product code.</summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>Quantity.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Unit price.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Total amount.</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Status.</summary>
        public WaybillStatus Status { get; set; }
    }
}
