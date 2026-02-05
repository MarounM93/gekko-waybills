namespace Gekko.Waybills.Application.Queries;

/// <summary>Totals for a delivery month.</summary>
public sealed class MonthlyTotalsDto
{
    /// <summary>Calendar year.</summary>
    public int Year { get; set; }

    /// <summary>Calendar month (1-12).</summary>
    public int Month { get; set; }

    /// <summary>Total quantity.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Total amount.</summary>
    public decimal TotalAmount { get; set; }
}
