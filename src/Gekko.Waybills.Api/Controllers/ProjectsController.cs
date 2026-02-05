using Gekko.Waybills.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Gekko.Waybills.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IWaybillQueryService _queryService;

    public ProjectsController(IWaybillQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>Returns all waybills for the specified project.</summary>
    /// <param name="id">Project identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns waybills for the project.</response>
    [HttpGet("{id:guid}/waybills")]
    [ProducesResponseType(typeof(List<WaybillListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WaybillListItemDto>>> GetWaybills(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetWaybillsByProjectAsync(id, cancellationToken);
        return Ok(result);
    }
}
