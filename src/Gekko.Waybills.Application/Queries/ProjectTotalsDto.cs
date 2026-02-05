namespace Gekko.Waybills.Application.Queries;

/// <summary>Totals grouped by project.</summary>
public sealed class ProjectTotalsDto
{
    /// <summary>Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Project name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Total quantity.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Total amount.</summary>
    public decimal TotalAmount { get; set; }
}
