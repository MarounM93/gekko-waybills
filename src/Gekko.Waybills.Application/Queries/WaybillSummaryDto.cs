namespace Gekko.Waybills.Application.Queries;

/// <summary>Aggregated waybill summaries.</summary>
public sealed class WaybillSummaryDto
{
    /// <summary>Totals by status.</summary>
    public List<StatusTotalsDto> StatusTotals { get; set; } = [];

    /// <summary>Totals grouped by delivery month.</summary>
    public List<MonthlyTotalsDto> MonthlyTotals { get; set; } = [];

    /// <summary>Top suppliers by quantity.</summary>
    public List<TopSupplierDto> TopSuppliersByQuantity { get; set; } = [];

    /// <summary>Totals grouped by project.</summary>
    public List<ProjectTotalsDto> ProjectTotals { get; set; } = [];
}
