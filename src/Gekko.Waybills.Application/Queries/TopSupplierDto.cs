namespace Gekko.Waybills.Application.Queries;

/// <summary>Top supplier by quantity.</summary>
public sealed class TopSupplierDto
{
    /// <summary>Supplier identifier.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Supplier name.</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Total quantity.</summary>
    public decimal TotalQuantity { get; set; }
}
