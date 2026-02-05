using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Application.Queries;

/// <summary>Query parameters for waybill listing.</summary>
public sealed class WaybillQueryParameters
{
    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Page size.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Waybill date range start.</summary>
    public DateOnly? WaybillDateFrom { get; set; }

    /// <summary>Waybill date range end.</summary>
    public DateOnly? WaybillDateTo { get; set; }

    /// <summary>Delivery date range start.</summary>
    public DateOnly? DeliveryDateFrom { get; set; }

    /// <summary>Delivery date range end.</summary>
    public DateOnly? DeliveryDateTo { get; set; }

    /// <summary>Waybill status filter.</summary>
    public WaybillStatus? Status { get; set; }

    /// <summary>Project identifier filter.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Supplier identifier filter.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Product code filter.</summary>
    public string? ProductCode { get; set; }

    /// <summary>Free-text search across project and supplier.</summary>
    public string? Search { get; set; }
}
