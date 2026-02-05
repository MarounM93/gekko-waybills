namespace Gekko.Waybills.Application.Queries;

/// <summary>Supplier summary totals.</summary>
public sealed class SupplierSummaryDto
{
    /// <summary>Supplier identifier.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Total quantity for the supplier.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Total amount for the supplier.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Totals grouped by status.</summary>
    public List<StatusTotalsDto> BreakdownByStatus { get; set; } = [];
}
