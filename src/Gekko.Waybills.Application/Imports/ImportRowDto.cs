namespace Gekko.Waybills.Application.Imports;

/// <summary>Represents a raw CSV row for a waybill import.</summary>
public sealed class ImportRowDto
{
    /// <summary>Tenant identifier from CSV, if provided.</summary>
    public string? TenantId { get; set; }

    /// <summary>Waybill number.</summary>
    public string? WaybillNumber { get; set; }

    /// <summary>Project name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Supplier name.</summary>
    public string? SupplierName { get; set; }

    /// <summary>Waybill date.</summary>
    public string? WaybillDate { get; set; }

    /// <summary>Delivery date.</summary>
    public string? DeliveryDate { get; set; }

    /// <summary>Product code.</summary>
    public string? ProductCode { get; set; }

    /// <summary>Quantity.</summary>
    public string? Quantity { get; set; }

    /// <summary>Unit price.</summary>
    public string? UnitPrice { get; set; }

    /// <summary>Total amount.</summary>
    public string? TotalAmount { get; set; }

    /// <summary>Waybill status.</summary>
    public string? Status { get; set; }
}
