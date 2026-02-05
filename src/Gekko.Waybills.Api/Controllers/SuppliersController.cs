using Gekko.Waybills.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Gekko.Waybills.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly IWaybillQueryService _queryService;

    public SuppliersController(IWaybillQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>Returns summary totals for the specified supplier.</summary>
    /// <param name="id">Supplier identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns supplier totals.</response>
    /// <response code="404">Supplier not found.</response>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(SupplierSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierSummaryDto>> GetSummary(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetSupplierSummaryAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
