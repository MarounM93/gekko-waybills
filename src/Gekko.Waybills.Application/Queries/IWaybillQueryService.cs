namespace Gekko.Waybills.Application.Queries;

/// <summary>Query service for waybill endpoints.</summary>
public interface IWaybillQueryService
{
    /// <summary>Returns paged waybills.</summary>
    Task<PagedResultDto<WaybillListItemDto>> GetWaybillsAsync(
        WaybillQueryParameters parameters,
        CancellationToken cancellationToken);

    /// <summary>Returns a single waybill by id.</summary>
    Task<WaybillDetailDto?> GetWaybillByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns all waybills for a project.</summary>
    Task<List<WaybillListItemDto>> GetWaybillsByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Returns summary totals for a supplier.</summary>
    Task<SupplierSummaryDto?> GetSupplierSummaryAsync(Guid supplierId, CancellationToken cancellationToken);

    /// <summary>Returns global summary for waybills.</summary>
    Task<WaybillSummaryDto> GetWaybillSummaryAsync(CancellationToken cancellationToken);
}
