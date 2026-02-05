using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Application.Queries;

/// <summary>Totals grouped by status.</summary>
public sealed class StatusTotalsDto
{
    /// <summary>Waybill status.</summary>
    public WaybillStatus Status { get; set; }

    /// <summary>Total quantity.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Total amount.</summary>
    public decimal TotalAmount { get; set; }
}
